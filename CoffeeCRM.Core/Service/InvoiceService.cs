using CoffeeCRM.Data.Model;
using CoffeeCRM.Core.Repository;
using CoffeeCRM.Core.Util;
using CoffeeCRM.Data;
using CoffeeCRM.Core.Util.Parameters;
using CoffeeCRM.Data.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoffeeCRM.Data.Constants;
using CoffeeCRM.Core.Repository.Interfaces;
using Microsoft.AspNetCore.SignalR;
using CoffeeCRM.Data.DTO;
using NetTopologySuite.Noding;
using CoffeeCRM.Core.Helper.VNPay;
using CoffeeCRM.Data.Enums.VNPay;
using CoffeeCRM.Data.VNPay;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeCRM.Core.Service
{
    public class InvoiceService : IInvoiceService
    {
        IInvoiceRepository invoiceRepository;
        IDishOrderRepository dishOrderRepository;
        IStockLevelRepository stockLevelRepository;
        IIngredientRepository ingredientRepository;
        IDishRepository dishRepository;
        IDishOrderDetailRepository dishOrderDetailRepository;
        IInvoiceDetailRepository invoiceDetailRepository;
        ITableRepository tableRepository;
        ICashFlowRepository cashFlowRepository;
        IAccountRepository accountRepository;
        INotificationRepository notificationRepository;
        IHubContext<NotificationHub, INotificationHub> notificationHub;
        IVNPayService _vnPayservice;
        IConfiguration _configuration;

        public InvoiceService(
        IInvoiceRepository _invoiceRepository,
        IDishRepository _dishRepository,
        IStockLevelRepository _stockLevelRepository,
        IIngredientRepository _ingredientRepository,
        IDishOrderRepository _dishOrderRepository,
        IDishOrderDetailRepository _dishOrderDetailRepository,
            IInvoiceDetailRepository _invoiceDetailRepository, ITableRepository _tableRepository, ICashFlowRepository _cashFlowRepository,
            IAccountRepository _accountRepository, INotificationRepository _notificationRepository,
            IHubContext<NotificationHub, INotificationHub> _notificationHub, IVNPayService vnPayservice, IConfiguration configuration)         
        {
            _configuration = configuration;
            _vnPayservice = vnPayservice;
            invoiceRepository = _invoiceRepository;
            dishRepository = _dishRepository;
            stockLevelRepository = _stockLevelRepository;
            ingredientRepository = _ingredientRepository;
            dishOrderRepository = _dishOrderRepository;
            dishOrderDetailRepository = _dishOrderDetailRepository;
            invoiceDetailRepository = _invoiceDetailRepository;
            tableRepository = _tableRepository;
            cashFlowRepository = _cashFlowRepository;
            accountRepository = _accountRepository;
            notificationRepository = _notificationRepository;
            notificationHub = _notificationHub;
            _vnPayservice.Initialize(_configuration["Vnpay:TmnCode"], _configuration["Vnpay:HashSecret"], _configuration["Vnpay:BaseUrl"], _configuration["Vnpay:CallbackUrl"]);
        }
        public async Task Add(Invoice obj)
        {
            obj.Active = true;
            obj.CreatedTime = DateTime.Now;
            await invoiceRepository.Add(obj);
        }

        public int Count()
        {
            var result = invoiceRepository.Count();
            return result;
        }

        public async Task Delete(Invoice obj)
        {
            obj.Active = false;
            await invoiceRepository.Delete(obj);
        }

        public async Task<long> DeletePermanently(long? id)
        {
            return await invoiceRepository.DeletePermanently(id);
        }

        public async Task<Invoice> Detail(long? id)
        {
            return await invoiceRepository.Detail(id);
        }

        public async Task<List<Invoice>> List()
        {
            return await invoiceRepository.List();
        }

        public async Task<object> GetTodaySummary()
        {
            var invoices = await invoiceRepository.List();

            var today = DateTime.Today;

            var todayInvoices = invoices
                .Where(x =>
                    x.Active == true &&
                    x.CreatedTime.Date == today)
                .ToList();

            var totalRevenue = todayInvoices.Sum(x => x.TotalMoney);
            var totalInvoices = todayInvoices.Count;

            decimal averageInvoice = totalInvoices > 0
                ? totalRevenue / totalInvoices
                : 0;

            return new
            {
                date = today.ToString("dd/MM/yyyy"),
                totalRevenue = totalRevenue,
                totalInvoices = totalInvoices,
                averageInvoice = averageInvoice
            };
        }

        public async Task<object> GetTopSellingDishes()
        {
            var invoiceDetails = await invoiceDetailRepository.List();
            var dishes = await dishRepository.List();

            var topDishes = invoiceDetails
                .Where(x => x.Active == true)
                .GroupBy(x => x.DishId)
                .Select(group =>
                {
                    var dish = dishes.FirstOrDefault(d => d.Id == group.Key);

                    var totalQuantity = group.Sum(x => x.Quantity);
                    var totalRevenue = group.Sum(x => x.Quantity * x.UnitPrice);

                    return new
                    {
                        dishId = group.Key,
                        dishName = dish != null
                            ? dish.DishName
                            : "Không xác định",

                        totalQuantity = totalQuantity,
                        totalRevenue = totalRevenue
                    };
                })
                .OrderByDescending(x => x.totalQuantity)
                .Take(5)
                .ToList();

            return topDishes;
        }

        public async Task<object> GetInventorySummary()
        {
            var stockLevels = await stockLevelRepository.List();
            var ingredients = await ingredientRepository.List();

            var today = DateTime.Today;
            var warningDate = today.AddDays(7);

            var inventory = stockLevels
                .Where(x => x.Active == true)
                .GroupBy(x => x.IngredientId)
                .Select(group =>
                {
                    var ingredient = ingredients
                        .FirstOrDefault(x => x.Id == group.Key);

                    var totalQuantity = group.Sum(x => x.Quantity);

                    var expiringSoon = group.Any(x =>
                        x.ExpirationDate >= today &&
                        x.ExpirationDate <= warningDate);

                    var expired = group.Any(x =>
                        x.ExpirationDate < today);

                    string status;

                    if (totalQuantity <= 0)
                    {
                        status = "OUT_OF_STOCK";
                    }
                    else if (totalQuantity <= 10)
                    {
                        status = "LOW_STOCK";
                    }
                    else
                    {
                        status = "NORMAL";
                    }

                    return new
                    {
                        ingredientId = group.Key,

                        ingredientName = ingredient != null
                            ? ingredient.IngredientName
                            : "Không xác định",

                        totalQuantity = totalQuantity,
                        status = status,
                        expiringSoon = expiringSoon,
                        expired = expired
                    };
                })
                .OrderBy(x => x.totalQuantity)
                .ToList();

            var lowStockCount = inventory.Count(x =>
                x.status == "LOW_STOCK");

            var outOfStockCount = inventory.Count(x =>
                x.status == "OUT_OF_STOCK");

            var expiringSoonCount = inventory.Count(x =>
                x.expiringSoon);

            var expiredCount = inventory.Count(x =>
                x.expired);

            return new
            {
                totalIngredients = inventory.Count,
                lowStockCount = lowStockCount,
                outOfStockCount = outOfStockCount,
                expiringSoonCount = expiringSoonCount,
                expiredCount = expiredCount,
                items = inventory
            };
        }


        public async Task<object> GetBusinessAnalysis()
        {
            var invoices = await invoiceRepository.List();
            var invoiceDetails = await invoiceDetailRepository.List();
            var dishes = await dishRepository.List();
            var stockLevels = await stockLevelRepository.List();

            var today = DateTime.Today;

            // =========================
            // DOANH THU HOM NAY
            // =========================

            var todayInvoices = invoices
                .Where(x =>
                    x.Active == true &&
                    x.CreatedTime.Date == today)
                .ToList();

            var totalRevenue = todayInvoices
                .Sum(x => x.TotalMoney);

            var totalInvoices = todayInvoices.Count;

            decimal averageInvoice =
                totalInvoices > 0
                    ? totalRevenue / totalInvoices
                    : 0;


            // =========================
            // MON BAN CHAY
            // =========================

            var topDish = invoiceDetails
                .Where(x => x.Active == true)
                .GroupBy(x => x.DishId)
                .Select(group =>
                {
                    var dish = dishes
                        .FirstOrDefault(x =>
                            x.Id == group.Key);

                    return new
                    {
                        dishName = dish != null
                            ? dish.DishName
                            : "Không xác định",

                        totalQuantity =
                            group.Sum(x => x.Quantity),

                        totalRevenue =
                            group.Sum(x =>
                                x.Quantity * x.UnitPrice)
                    };
                })
                .OrderByDescending(x =>
                    x.totalQuantity)
                .FirstOrDefault();


            // =========================
            // TON KHO
            // =========================

            var stockGroups = stockLevels
                .Where(x => x.Active == true)
                .GroupBy(x => x.IngredientId)
                .Select(group => new
                {
                    IngredientId = group.Key,
                    TotalQuantity =
                        group.Sum(x => x.Quantity),

                    ExpiringSoon = group.Any(x =>
                        x.ExpirationDate >= today &&
                        x.ExpirationDate <=
                            today.AddDays(7)),

                    Expired = group.Any(x =>
                        x.ExpirationDate < today)
                })
                .ToList();


            var lowStockCount =
                stockGroups.Count(x =>
                    x.TotalQuantity > 0 &&
                    x.TotalQuantity <= 10);

            var outOfStockCount =
                stockGroups.Count(x =>
                    x.TotalQuantity <= 0);

            var expiringSoonCount =
                stockGroups.Count(x =>
                    x.ExpiringSoon);

            var expiredCount =
                stockGroups.Count(x =>
                    x.Expired);


            // =========================
            // KET QUA
            // =========================

            return new
            {
                date = today.ToString("dd/MM/yyyy"),

                revenue = new
                {
                    totalRevenue = totalRevenue,
                    totalInvoices = totalInvoices,
                    averageInvoice = averageInvoice
                },

                topDish = topDish,

                inventory = new
                {
                    lowStockCount = lowStockCount,
                    outOfStockCount = outOfStockCount,
                    expiringSoonCount = expiringSoonCount,
                    expiredCount = expiredCount
                }
            };
        }

        public async Task<List<Invoice>> ListPaging(int pageIndex, int pageSize)
        {
            return await invoiceRepository.ListPaging(pageIndex, pageSize);
        }

        public async Task<DTResult<Invoice>> ListServerSide(InvoiceDTParameters parameters)
        {
            return await invoiceRepository.ListServerSide(parameters);
        }

        public async Task Update(Invoice obj)
        {
            await invoiceRepository.Update(obj);
        }

        public async Task<InvoiceViewModel> AddByVm(InvoiceViewModel model)
        {
            using (var transaction = invoiceRepository.GetDatabase().BeginTransaction())
            {
                try
                {
                    var invoiceId = model.Id;
                    var currentYear = DateTime.Now.Year;
                    var lastInvoice = await invoiceRepository.getLastestInvoice();
                    // Lấy ID tiếp theo (hoặc bắt đầu từ 1 nếu là hóa đơn đầu tiên của năm)
                    int nextId;
                    if (lastInvoice != null)
                    {
                        string[] parts = lastInvoice.InvoiceCode.Split('-');

                        // Kiểm tra xem phần tử cuối có thể chuyển thành int không
                        if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastId))
                        {
                            nextId = lastId + 1;
                        }
                        else
                        {
                            nextId = 1; // Reset ID nếu không tìm thấy hoặc không thể parse
                        }
                    }
                    else
                    {
                        nextId = 1; // Nếu chưa có hóa đơn nào trong năm
                    }
                    // Tạo mã hóa đơn mới
                    string newInvoiceCode = $"HD-{currentYear}-{nextId}";
                    if (model.Id == 0) // Nếu Id = 0 thì thêm mới
                    {
                        var newInvoice = new Invoice();
                        newInvoice.InvoiceCode = newInvoiceCode;
                        newInvoice.AccountId = model.AccountId;
                        newInvoice.PaymentMethod = model.PaymentMethod;
                        newInvoice.PaymentStatus = PaymentStatusStringConst.PAID;
                        newInvoice.TotalMoney = model.TotalMoney;
                        newInvoice.CreatedTime = DateTime.Now;
                        newInvoice.Active = true;
                        await invoiceRepository.Add(newInvoice);
                        if (newInvoice.Id <= 0)
                        {
                            await transaction.RollbackAsync();
                            return null;
                        }
                        invoiceId = newInvoice.Id;
                    }
                    var listInvoiceDetail = model.InvoiceDetails;
                    var listCrrInvoiceDetail = await invoiceDetailRepository.ListByInvoideId(invoiceId);
                    if (listInvoiceDetail == null || listInvoiceDetail.Count <= 0)
                    {
                        await transaction.RollbackAsync();
                        return null;
                    }
                    foreach (var item in listInvoiceDetail)
                    {
                        if (item.Id == 0)
                        {
                            var newInvoiceDetail = item;
                            newInvoiceDetail.InvoiceId = invoiceId;
                            newInvoiceDetail.Quantity = item.Quantity;
                            newInvoiceDetail.UnitPrice = item.UnitPrice;
                            newInvoiceDetail.DishId = item.DishId;
                            newInvoiceDetail.CreatedTime = DateTime.Now;
                            newInvoiceDetail.Active = true;
                            await invoiceDetailRepository.Add(newInvoiceDetail);
                            if (newInvoiceDetail.Id <= 0)
                            {
                                await transaction.RollbackAsync();
                                return null;
                            }

                        }
                    }
                    CashFlow cflow = new CashFlow();
                    cflow.TotalMoney = model.TotalMoney;
                    cflow.FlowType = CashFlowConst.CASH_FLOW_TYPE_INCOME;
                    cflow.Note = "Hóa đơn bán hàng";
                    cflow.CreatedTime = DateTime.Now;
                    cflow.Active = true;
                    cflow.AccountId = model.AccountId;
                    await cashFlowRepository.Add(cflow);
                    if (cflow.Id <= 0)
                    {
                        await transaction.RollbackAsync();
                        return null;
                    }

                    bool isExist;
                    foreach (var item in listCrrInvoiceDetail)
                    {
                        isExist = false;
                        foreach (var i in listInvoiceDetail)
                        {
                            if (item.Id == i.Id) isExist = true;
                        }
                        if (!isExist)
                        {
                            var od = new InvoiceDetail();
                            od.Id = item.Id;
                            od.Active = false;
                            await invoiceDetailRepository.Delete(od);
                        }
                    }                 
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return null;
                }
                await transaction.CommitAsync();
                return model;
            }
        }

        public async Task<InvoiceViewModel> AddOrUpdateVM(InvoiceViewModel model)
        {
            using (var transaction = invoiceRepository.GetDatabase().BeginTransaction())
            {
                var invoiceCode = "";
                var accountId = 0;
                var invoiceId = model.Id;
                try
                {                                   
                    if (model.Id > 0)
                    {
                        var invoice = await invoiceRepository.Detail(model.Id);
                        if(invoice == null)
                        {
                            await transaction.RollbackAsync();
                            return null;
                        }
                        //invoice.PaymentMethod = model.PaymentMethod;
                        invoice.PaymentStatus = model.PaymentStatus; // calcel or paid
                        //invoice.TotalMoney = model.TotalMoney;
                        
                        await invoiceRepository.Update(invoice);
                        invoiceCode = invoice.InvoiceCode;
                        accountId = invoice.AccountId;
                    }
                    else if(model.Id == 0)
                    {
                        var currentYear = DateTime.Now.Year;
                        var lastInvoice = await invoiceRepository.getLastestInvoice();
                        // Lấy ID tiếp theo (hoặc bắt đầu từ 1 nếu là hóa đơn đầu tiên của năm)
                        int nextId;
                        if (lastInvoice != null)
                        {
                            string[] parts = lastInvoice.InvoiceCode.Split('-');

                            // Kiểm tra xem phần tử cuối có thể chuyển thành int không
                            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastId))
                            {
                                nextId = lastId + 1;
                            }
                            else
                            {
                                nextId = 1; // Reset ID nếu không tìm thấy hoặc không thể parse
                            }
                        }
                        else
                        {
                            nextId = 1; // Nếu chưa có hóa đơn nào trong năm
                        }
                        // Tạo mã hóa đơn mới
                        string newInvoiceCode = $"HD-{currentYear}-{nextId}";

                        var invoice = new Invoice();
                        invoice.InvoiceCode = newInvoiceCode;
                        invoice.AccountId = model.AccountId;
                        invoice.PaymentMethod = model.PaymentMethod;
                        invoice.PaymentStatus = PaymentStatusStringConst.PENDING;
                        invoice.TotalMoney = model.TotalMoney;
                        invoice.TableId = model.TableId;
                        invoice.CreatedTime = DateTime.Now;
                        invoice.Active = true;
                        await invoiceRepository.Add(invoice);
                        if (invoice.Id <= 0)
                        {
                            await transaction.RollbackAsync();
                            return null;
                        }
                        invoiceId = invoice.Id;
                        invoiceCode = invoice.InvoiceCode;
                        accountId = invoice.AccountId;
                    }

                    var details = model.InvoiceDetails;
                    var listCrrInvoiceDetail = await invoiceDetailRepository.ListByInvoideId(invoiceId);

                    if (details == null || details.Count <= 0)
                    {
                        await transaction.RollbackAsync();
                        return null;
                    }

                    foreach (var item in details)
                    {
                        if(item.Id == 0)
                        {
                            var newInvoiceDetail = new InvoiceDetail();
                            newInvoiceDetail.InvoiceId = invoiceId;
                            newInvoiceDetail.Quantity = item.Quantity;
                            newInvoiceDetail.UnitPrice = item.UnitPrice;
                            newInvoiceDetail.DishId = item.DishId;
                            newInvoiceDetail.CreatedTime = DateTime.Now;
                            newInvoiceDetail.Active = true;
                            await invoiceDetailRepository.Add(newInvoiceDetail);
                            if (newInvoiceDetail.Id <= 0)
                            {
                                await transaction.RollbackAsync();
                                return null;
                            }
                        }
                        else if(item.Id > 0)
                        {
                            var invoiceDetail = await invoiceDetailRepository.Detail(item.Id);
                            if (invoiceDetail == null)
                            {
                                await transaction.RollbackAsync();
                                return null;
                            }
                            invoiceDetail.Quantity = item.Quantity;
                            invoiceDetail.UnitPrice = item.UnitPrice;
                            invoiceDetail.DishId = item.DishId;
                            invoiceDetail.Active = true;
                            await invoiceDetailRepository.Update(invoiceDetail);
                        }
                    }

                    //bool isExist;
                    //foreach (var item in listCrrInvoiceDetail)
                    //{
                    //    isExist = false;
                    //    foreach (var i in details)
                    //    {
                    //        if (item.Id == i.Id) isExist = true;
                    //    }
                    //    if (!isExist)
                    //    {
                    //        var od = new InvoiceDetail();
                    //        od.Id = item.Id;
                    //        od.Active = false;
                    //        await invoiceDetailRepository.Delete(od);
                    //    }
                    //}

                    if (model.PaymentStatus == PaymentStatusStringConst.PAID)
                    {
                        CashFlow cflow = new CashFlow();
                        cflow.TotalMoney = model.TotalMoney;
                        cflow.FlowType = CashFlowConst.CASH_FLOW_TYPE_INCOME;
                        cflow.Note = "Hóa đơn bán hàng";
                        cflow.CreatedTime = DateTime.Now;
                        cflow.Active = true;
                        cflow.AccountId = model.AccountId;
                        await cashFlowRepository.Add(cflow);
                        if (cflow.Id <= 0)
                        {
                            await transaction.RollbackAsync();
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return null;
                }

                var listOldDishOrder = await dishOrderRepository.ListUnPaid(model.TableId);
                foreach (var item in listOldDishOrder)
                {
                    item.DishOrderStatusId = DishOrderStatudConst.PAID;
                    if (!await dishOrderRepository.Update(item))
                    {
                        await transaction.RollbackAsync();
                        return null;
                    }
                }

                var table = await tableRepository.Detail(model.TableId);
                if(table == null)
                {
                    await transaction.RollbackAsync();
                    return null;
                }
                table.TableStatus = TableConst.AVAILABLE;
                await tableRepository.Update(table);

                var accountName = await accountRepository.Detail(accountId);
                var listAdmin = await accountRepository.GetByRoleId(RoleConst.ADMIN);
                var ids = listAdmin.Select(x => x.Id).ToList();
                ids.Add(accountId);
                var mess = model.Id > 0 ? $"{accountName} đã cập nhật hoá đơn {invoiceCode}" : $"{accountName} đã tạo mới hoá đơn {invoiceCode}";

                foreach (var id in ids)
                {
                    var noti = new Notification();
                    noti.AccountId = id;
                    noti.NotificationStatusId = NotificationStatusId.Unread;
                    noti.Name = mess;
                    noti.Description = mess;
                    noti.SenderId = accountId;
                    noti.CreatedTime = DateTime.Now;
                    noti.Active = true;
                    noti.Url = $"/invoice/detail/{invoiceId}";
                    await notificationHub.Clients.All.ReceiveNotification(noti);
                }

                await transaction.CommitAsync();
                
                model.Id = invoiceId;
                return model;
            }
        }
        public async Task<InvoiceVM> InvoiceDetailById(int invoiceId)
        {
            return await invoiceRepository.InvoiceDetailById(invoiceId);
        }

        public async Task<InvoiceViewModel> UpdateStatus(InvoiceViewModel model)
        {
            using (var transaction = invoiceRepository.GetDatabase().BeginTransaction())
            {
                try
                {
                    if (model.Id <= 0)
                    {
                        throw new BadRequestException("ID không tồn tại");
                    }

                    var invoice = await invoiceRepository.Detail(model.Id);
                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();
                        return null;
                    }
                    //invoice.PaymentMethod = model.PaymentMethod;
                    invoice.PaymentStatus = model.PaymentStatus; 
                    invoice.PaymentMethod = model.PaymentMethod; 

                    await invoiceRepository.Update(invoice);

                    if (model.PaymentStatus == PaymentStatusStringConst.PAID)
                    {
                        CashFlow cflow = new CashFlow();
                        cflow.TotalMoney = model.TotalMoney;
                        cflow.FlowType = CashFlowConst.CASH_FLOW_TYPE_INCOME;
                        cflow.Note = "Hóa đơn bán hàng";
                        cflow.CreatedTime = DateTime.Now;
                        cflow.Active = true;
                        cflow.AccountId = model.AccountId;
                        await cashFlowRepository.Add(cflow);
                        if (cflow.Id <= 0)
                        {
                            await transaction.RollbackAsync();
                            return null;
                        }
                    }

                    await transaction.CommitAsync();
                    return model;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return null;
                }
            }
        }

        public async Task PaymentSuccess(int id, string transCode)
        {
            // invoice - invoiceCode
            using (var transaction = invoiceRepository.GetDatabase().BeginTransaction())
            {
                try
                {
                    if (id <= 0)
                    {
                        throw new BadRequestException("ID không tồn tại");
                    }

                    var invoice = await invoiceRepository.Detail(id);
                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();
                        return;
                    }

                    invoice.InvoiceCode = transCode != "" ? transCode : ""; // Cập nhật mã giao dịch từ VNPay
                    invoice.PaymentStatus = PaymentStatusStringConst.PAID;
                    await invoiceRepository.Update(invoice);

                    CashFlow cflow = new CashFlow();
                    cflow.TotalMoney = invoice.TotalMoney;
                    cflow.FlowType = CashFlowConst.CASH_FLOW_TYPE_INCOME;
                    cflow.Note = "Hóa đơn bán hàng";
                    cflow.CreatedTime = DateTime.Now;
                    cflow.Active = true;
                    cflow.AccountId = 1;
                    await cashFlowRepository.Add(cflow);
                    if (cflow.Id <= 0)
                    {
                        await transaction.RollbackAsync();
                        return;
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return;
                }
            }
        }
    }
}

