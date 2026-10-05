

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoffeeCRM.Data.Model;
using CoffeeCRM.Core.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CoffeeCRM.Core.Util;
using CoffeeCRM.Data.Constants;
using CoffeeCRM.Core.Service;
using CoffeeCRM.Core.Util.Parameters;
using CoffeeCRM.Data.ViewModels;
using CoffeeCRM.Core.Helper.VNPay;
using CoffeeCRM.Data.Enums.VNPay;
using CoffeeCRM.Data.VNPay;
using Internal;

namespace CfCRM.DATN.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        IInvoiceService service;
        IVNPayService _vnPayservice;
        IConfiguration _configuration;
        public InvoiceController(IInvoiceService _service, IVNPayService vnPayservice, IConfiguration configuration)
        {
            service = _service;
            _vnPayservice = vnPayservice;
            _configuration = configuration;
            _vnPayservice.Initialize(_configuration["Vnpay:TmnCode"], _configuration["Vnpay:HashSecret"], _configuration["Vnpay:BaseUrl"], _configuration["Vnpay:CallbackUrl"]);
        }

        [HttpGet]
        [Route("api/List")]
        public async Task<IActionResult> List()
        {
            try
            {
                var dataList = await service.List();
                if (dataList == null || dataList.Count == 0)
                {
                    return NotFound();
                }
                var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(dataList.Cast<object>().ToList());
                return Ok(coffeemanagementResponse);
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }

        [HttpGet]
        [Route("api/Detail/{Id}")]
        public async Task<IActionResult> Detail(int? Id)
        {
            if (Id == null)
            {
                return BadRequest();
            }
            try
            {
                var dataList = await service.Detail(Id);
                if (dataList == null)
                {
                    return NotFound();
                }
                var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(dataList);
                return Ok(coffeemanagementResponse);
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }

        [HttpPost]
        [Route("api/ai/chat")]
        public async Task<IActionResult> AiChat([FromBody] AiChatRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest("Vui lòng nhập câu hỏi.");
                }

                var apiKey = _configuration["Gemini:ApiKey"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return BadRequest("Chưa cấu hình Gemini API Key.");
                }

                // Lấy dữ liệu thực tế từ CoffeeCRM
                var businessData = await service.GetBusinessAnalysis();

                var businessJson = System.Text.Json.JsonSerializer.Serialize(
                    businessData
                );

                var prompt = $@"
                Bạn là trợ lý AI của hệ thống quản lý quán CoffeeCRM.

                NHIỆM VỤ:
                - Trả lời bằng tiếng Việt.
                - Trả lời ngắn gọn, rõ ràng, dễ hiểu.
                - Ưu tiên dữ liệu thực tế được cung cấp từ CoffeeCRM.
                - Không tự bịa doanh thu, số lượng, món ăn hoặc tồn kho.
                - Nếu dữ liệu không đủ để kết luận, hãy nói rõ chưa đủ dữ liệu.
                - Khi có vấn đề tồn kho, hãy cảnh báo người quản lý.
                - Có thể đưa ra nhận xét và gợi ý vận hành dựa trên dữ liệu.
                - Không được yêu cầu hoặc tiết lộ API key.
                - Không được tự ý thay đổi dữ liệu CoffeeCRM.

                DỮ LIỆU COFFEECRM HIỆN TẠI:
                {businessJson}

                CÂU HỎI CỦA NGƯỜI DÙNG:
                {request.Message}
                ";

                using var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Add(
                    "x-goog-api-key",
                    apiKey
                );

                var geminiRequest = new
                {
                    contents = new[]
                    {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(
                    geminiRequest
                );

                var content = new StringContent(
                    json,
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var models = new[]
{
    "gemini-3.8-flash",
    "gemini-3.7-flash",
    "gemini-3.5-flash-lite"
};

                string? responseText = null;
                string? usedModel = null;
                int lastStatusCode = 0;

                foreach (var model in models)
                {
                    try
                    {
                        var url =
                            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

                        // Phải tạo content mới cho mỗi lần thử
                        var requestContent = new StringContent(
                            json,
                            System.Text.Encoding.UTF8,
                            "application/json"
                        );

                        var response = await httpClient.PostAsync(
                            url,
                            requestContent
                        );

                        lastStatusCode = (int)response.StatusCode;

                        var currentResponseText =
                            await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            responseText = currentResponseText;
                            usedModel = model;
                            break;
                        }

                        Console.WriteLine(
                            $"Gemini model {model} lỗi " +
                            $"{lastStatusCode}: {currentResponseText}"
                        );
                    }
                    catch (Exception modelException)
                    {
                        Console.WriteLine(
                            $"Gemini model {model} exception: " +
                            modelException.Message
                        );
                    }
                }

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "Các model AI hiện đều không khả dụng. Vui lòng thử lại sau.",
                        statusCode = lastStatusCode
                    });
                }

              
                using var document =
                    System.Text.Json.JsonDocument.Parse(responseText);

                var root = document.RootElement;

                if (!root.TryGetProperty(
                        "candidates",
                        out var candidates) ||
                    candidates.GetArrayLength() == 0)
                {
                    return BadRequest(
                        "Gemini không trả về nội dung."
                    );
                }

                var parts = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts");

                var answerParts = new List<string>();

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty(
                            "text",
                            out var textElement))
                    {
                        answerParts.Add(
                            textElement.GetString() ?? ""
                        );
                    }
                }

                var answer = string.Join(
                    "\n",
                    answerParts
                );

                return Ok(new
                {
                    success = true,
                    answer = answer,
                    model = usedModel
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpGet]
        [Route("api/ListPaging")]
        public async Task<IActionResult> ListPaging(int pageIndex, int pageSize)
        {
            if (pageIndex < 0 || pageSize < 0) return BadRequest();
            try
            {
                var dataList = await service.ListPaging(pageIndex, pageSize);

                if (dataList == null || dataList.Count == 0)
                {
                    return NotFound();
                }

                var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(dataList.Cast<object>().ToList());
                return Ok(coffeemanagementResponse);
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }
        [HttpPost]
        [Route("api/Add")]
        public async Task<IActionResult> Add([FromBody] Invoice model)
        {
            if (ModelState.IsValid)
            {
                //1. business logic

                //data validation
                if (model.Active == false)
                {
                    return BadRequest();
                }
                //2. add new object
                try
                {
                    await service.Add(model);
                    var coffeemanagementResponse = CoffeeManagementResponse.CREATED(model);
                    return Created("", coffeemanagementResponse);
                }
                catch (Exception)
                {

                    return BadRequest();
                }
            }
            return BadRequest();
        }


        [HttpPost]
        [Route("api/Update")]
        public async Task<IActionResult> Update([FromBody] Invoice model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    //1. business logic 
                    //2. update object
                    await service.Update(model);
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(model);
                    return Ok(coffeemanagementResponse);
                }
                catch (Exception ex)
                {
                    if (ex.GetType().FullName == "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")
                    {
                        return NotFound();
                    }
                    return BadRequest();
                }
            }
            return BadRequest();
        }
        [HttpPost]
        [Route("api/Delete")]
        public async Task<IActionResult> Delete([FromBody] Invoice model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    //1. business logic
                    await service.Delete(model);
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(model);
                    return Ok(coffeemanagementResponse);
                }
                catch (Exception ex)
                {
                    if (ex.GetType().FullName == "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")
                    {
                        return NotFound();
                    }
                    return BadRequest();
                }
            }
            return BadRequest();
        }
        [HttpPost]
        [Route("api/DeletePermanently")]
        public async Task<IActionResult> DeletePermanently([FromBody] Invoice model)
        {
            var result = 0;
            if (!(model.Id > 0))
            {
                return BadRequest();
            }
            try
            {
                //physically delete object
                result = (int)await service.DeletePermanently(model.Id);
                if (result == 0)
                {
                    return NotFound();
                }
                var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(model);
                return Ok(coffeemanagementResponse);
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }


        [HttpGet]
        [Route("api/Count")]
        public int CountInvoice()
        {
            int result = service.Count();
            return result;
        }

        [HttpGet]
        [Route("api/ai/today-summary")]
        public async Task<IActionResult> GetTodaySummary()
        {
            try
            {
                var data = await service.GetTodaySummary();
                return Ok(CoffeeManagementResponse.SUCCESS(data));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("api/ai/top-dishes")]
        public async Task<IActionResult> GetTopSellingDishes()
        {
            try
            {
                var data = await service.GetTopSellingDishes();

                return Ok(
                    CoffeeManagementResponse.SUCCESS(data)
                );
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("api/ai/inventory")]
        public async Task<IActionResult> GetInventorySummary()
        {
            try
            {
                var data = await service.GetInventorySummary();

                return Ok(
                    CoffeeManagementResponse.SUCCESS(data)
                );
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpGet]
        [Route("api/ai/business-analysis")]
        public async Task<IActionResult> GetBusinessAnalysis()
        {
            try
            {
                var data = await service.GetBusinessAnalysis();

                return Ok(
                    CoffeeManagementResponse.SUCCESS(data)
                );
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        [HttpPost]
        [Route("api/list-server-side")]
        public async Task<IActionResult> ListServerSide([FromBody] InvoiceDTParameters parameters)
        {
            try
            {
                var data = await service.ListServerSide(parameters);
                return Ok(data);
            }
            catch (Exception e)
            {
                return BadRequest(e);
            }
        }

        [HttpPost]
        [Route("api/AddOrUpdateVM")]
        public async Task<IActionResult> AddOrUpdateVM([FromBody] InvoiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var invoiceCreated = await service.AddOrUpdateVM(model);
                    if (invoiceCreated == null)
                    {
                        return BadRequest();
                    }
                    if (model.PaymentMethod == PaymentMethodConst.CASH)
                    {
                        return Created("", CoffeeManagementResponse.SUCCESS(invoiceCreated));
                    }
                    var ipAddress = NetworkHelper.GetIpAddress(HttpContext); // Lấy địa chỉ IP của thiết bị thực hiện giao dịch
                    var request = new PaymentRequest
                    {
                        PaymentId = DateTime.Now.Ticks,
                        Money = (double)model.TotalMoney,
                        Description = model.Id.ToString(),
                        IpAddress = ipAddress,
                        BankCode = BankCode.ANY, // Tùy chọn. Mặc định là tất cả phương thức giao dịch
                        CreatedDate = DateTime.Now, // Tùy chọn. Mặc định là thời điểm hiện tại
                        Currency = Currency.VND, // Tùy chọn. Mặc định là VND (Việt Nam đồng)
                        Language = DisplayLanguage.Vietnamese // Tùy chọn. Mặc định là tiếng Việt
                    };

                    var paymentUrl = _vnPayservice.GetPaymentUrl(request);
                    invoiceCreated.UriVnPay = paymentUrl;
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(invoiceCreated);
                    return Created("", coffeemanagementResponse);
                }
                catch (Exception)
                {

                    return BadRequest();
                }
            }
            return BadRequest();
        }

        [HttpPost]
        [Route("api/UpdateStatus")]
        public async Task<IActionResult> UpdateStatus([FromBody] InvoiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var invoiceCreated = await service.UpdateStatus(model);
                    if (invoiceCreated == null)
                    {
                        return BadRequest();
                    }
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(invoiceCreated);
                    return Created("", coffeemanagementResponse);
                }
                catch (Exception)
                {

                    return BadRequest();
                }
            }
            return BadRequest();
        }

        [HttpGet]
        [Route("api/InvoiceDetailVM")]
        public async Task<IActionResult> DetailVM(int invoiceId)
        {
            if (ModelState.IsValid)
            {
                try
                {

                    var result = await service.InvoiceDetailById(invoiceId);
                    if (result == null)
                    {
                        return BadRequest();
                    }
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS(result);
                    return Created("", coffeemanagementResponse);
                }
                catch (Exception)
                {

                    return BadRequest();
                }
            }
            return BadRequest();
        }

        [HttpPost]
        [Route("api/PaymentSuccess")]
        public async Task<IActionResult> PaymentSuccess(int id, string invoiceCode)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await service.PaymentSuccess(id, invoiceCode);
                    
                    var coffeemanagementResponse = CoffeeManagementResponse.SUCCESS();
                    return Created("", coffeemanagementResponse);
                }
                catch (Exception)
                {

                    return BadRequest();
                }
            }
            return BadRequest();
        }
    }
    public class AiChatRequest
    {
        public string Message { get; set; } = "";
    }
}
