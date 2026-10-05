USE [Cf.CRM.NCKH.v02];
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRAN;
DECLARE @Now datetime=GETDATE();

-- Dish categories
IF NOT EXISTS(SELECT 1 FROM dbo.DishCategory WHERE DishCategoryCode='COFFEE')
 INSERT dbo.DishCategory(DishCategoryCode,DishCateogryName,CreatedTime,Active) VALUES('COFFEE',N'Cà phê',@Now,1);
IF NOT EXISTS(SELECT 1 FROM dbo.DishCategory WHERE DishCategoryCode='TEA')
 INSERT dbo.DishCategory(DishCategoryCode,DishCateogryName,CreatedTime,Active) VALUES('TEA',N'Trà',@Now,1);
IF NOT EXISTS(SELECT 1 FROM dbo.DishCategory WHERE DishCategoryCode='ICEBLEND')
 INSERT dbo.DishCategory(DishCategoryCode,DishCateogryName,CreatedTime,Active) VALUES('ICEBLEND',N'Đá xay',@Now,1);
IF NOT EXISTS(SELECT 1 FROM dbo.DishCategory WHERE DishCategoryCode='JUICE')
 INSERT dbo.DishCategory(DishCategoryCode,DishCateogryName,CreatedTime,Active) VALUES('JUICE',N'Nước ép',@Now,1);
IF NOT EXISTS(SELECT 1 FROM dbo.DishCategory WHERE DishCategoryCode='OTHER')
 INSERT dbo.DishCategory(DishCategoryCode,DishCateogryName,CreatedTime,Active) VALUES('OTHER',N'Đồ uống khác',@Now,1);

DECLARE @D TABLE(Code nvarchar(255),Name nvarchar(100),Price decimal(18,0),Cat nvarchar(255),Photo nvarchar(255));
INSERT @D VALUES
('CF001',N'Cà phê đen',30000,'COFFEE','/assets/media/dishes/coffee.jpg'),
('CF002',N'Cà phê sữa',35000,'COFFEE','/assets/media/dishes/coffee.jpg'),
('CF003',N'Bạc xỉu',39000,'COFFEE','/assets/media/dishes/coffee.jpg'),
('CF004',N'Americano',40000,'COFFEE','/assets/media/dishes/coffee.jpg'),
('CF005',N'Latte',49000,'COFFEE','/assets/media/dishes/coffee.jpg'),
('TE001',N'Trà đào cam sả',45000,'TEA','/assets/media/dishes/tea.jpg'),
('TE002',N'Trà vải',45000,'TEA','/assets/media/dishes/tea.jpg'),
('TE003',N'Trà chanh',35000,'TEA','/assets/media/dishes/tea.jpg'),
('IB001',N'Matcha đá xay',55000,'ICEBLEND','/assets/media/dishes/iceblend.jpg'),
('IB002',N'Chocolate đá xay',55000,'ICEBLEND','/assets/media/dishes/iceblend.jpg'),
('JU001',N'Nước cam',45000,'JUICE','/assets/media/dishes/juice.jpg'),
('OT001',N'Nước suối',20000,'OTHER','/assets/media/dishes/water.jpg');
INSERT dbo.Dish(DishCode,DishName,Price,Photo,CreatedTime,Active,DishCategoryId)
SELECT d.Code,d.Name,d.Price,d.Photo,@Now,1,c.Id FROM @D d
JOIN dbo.DishCategory c ON c.DishCategoryCode=d.Cat
WHERE NOT EXISTS(SELECT 1 FROM dbo.Dish x WHERE x.DishCode=d.Code);

-- Order statuses
DECLARE @S TABLE(Code nvarchar(255),Name nvarchar(255));
INSERT @S VALUES ('PENDING',N'Chờ xác nhận'),('PROCESSING',N'Đang pha chế'),('READY',N'Sẵn sàng phục vụ'),('COMPLETED',N'Hoàn thành'),('CANCELLED',N'Đã hủy');
INSERT dbo.DishOrderStatus(DishOrderStatusCode,DishOrderStatusName,CreatedTime,Active)
SELECT s.Code,s.Name,@Now,1 FROM @S s WHERE NOT EXISTS(SELECT 1 FROM dbo.DishOrderStatus x WHERE x.DishOrderStatusCode=s.Code);

-- Units
DECLARE @U TABLE(Code nvarchar(255),Name nvarchar(255));
INSERT @U VALUES('KG',N'Kg'),('G',N'Gram'),('L',N'Lít'),('ML',N'Ml'),('BOX',N'Hộp'),('BOTTLE',N'Chai');
INSERT dbo.Unit(UnitCode,UnitName,CreatedTime,Active)
SELECT u.Code,u.Name,@Now,1 FROM @U u WHERE NOT EXISTS(SELECT 1 FROM dbo.Unit x WHERE x.UnitCode=u.Code);

-- Suppliers
DECLARE @SP TABLE(Code nvarchar(255),Name nvarchar(255),Contact nvarchar(50),Addr nvarchar(300));
INSERT @SP VALUES
('NCC001',N'Nhà cung cấp cà phê','0901000001',N'Hà Nội'),
('NCC002',N'Nhà cung cấp sữa và đồ uống','0901000002',N'Hà Nội'),
('NCC003',N'Nhà cung cấp trái cây','0901000003',N'Hà Nội');
INSERT dbo.Supplier(SupplierCode,ContactInfo,SupplierName,Address,CreatedTime,Active)
SELECT s.Code,s.Contact,s.Name,s.Addr,@Now,1 FROM @SP s WHERE NOT EXISTS(SELECT 1 FROM dbo.Supplier x WHERE x.SupplierCode=s.Code);

-- Ingredient categories
DECLARE @IC TABLE(Code nvarchar(255),Name nvarchar(100));
INSERT @IC VALUES('IC_COFFEE',N'Cà phê'),('IC_MILK',N'Sữa'),('IC_TEA',N'Trà'),('IC_FRUIT',N'Trái cây'),('IC_OTHER',N'Nguyên liệu khác');
INSERT dbo.IngredientCategory(IngredientCategoryCode,IngredientCategoryName,CreatedTime,Active,parentCategory)
SELECT c.Code,c.Name,@Now,1,NULL FROM @IC c WHERE NOT EXISTS(SELECT 1 FROM dbo.IngredientCategory x WHERE x.IngredientCategoryCode=c.Code);

-- Ingredients
DECLARE @I TABLE(Code nvarchar(255),Name nvarchar(100),Life int,Price decimal(18,0),Cat nvarchar(255),Sup nvarchar(255),UnitCode nvarchar(255));
INSERT @I VALUES
('NL001',N'Hạt cà phê',180,250000,'IC_COFFEE','NCC001','KG'),
('NL002',N'Sữa đặc',365,65000,'IC_MILK','NCC002','BOX'),
('NL003',N'Sữa tươi',14,38000,'IC_MILK','NCC002','L'),
('NL004',N'Trà đen',365,180000,'IC_TEA','NCC001','KG'),
('NL005',N'Cam tươi',10,45000,'IC_FRUIT','NCC003','KG');
INSERT dbo.Ingredient(IngredientCode,IngredientName,SelfLife,AveragePrice,CreatedTime,Active,IngredientCategoryId,SupplierId,UnitId)
SELECT i.Code,i.Name,i.Life,i.Price,@Now,1,c.Id,s.Id,u.Id FROM @I i
JOIN dbo.IngredientCategory c ON c.IngredientCategoryCode=i.Cat
JOIN dbo.Supplier s ON s.SupplierCode=i.Sup JOIN dbo.Unit u ON u.UnitCode=i.UnitCode
WHERE NOT EXISTS(SELECT 1 FROM dbo.Ingredient x WHERE x.IngredientCode=i.Code);

-- Warehouses + stock
IF NOT EXISTS(SELECT 1 FROM dbo.Warehouse WHERE WarehouseCode='WH001')
 INSERT dbo.Warehouse(WarehouseCode,WarehouseName,Location,Note,CreatedTime,Active) VALUES('WH001',N'Kho nguyên liệu chính',N'Tầng 1',N'Kho demo',@Now,1);
DECLARE @WH int=(SELECT TOP 1 Id FROM dbo.Warehouse WHERE WarehouseCode='WH001');
INSERT dbo.StockLevel(Quantity,ExpirationDate,UnitPrice,CreatedTime,Active,IngredientId,WarehouseId,LastUpdatedTime)
SELECT 100,DATEADD(day,90,@Now),ISNULL(i.AveragePrice,0),@Now,1,i.Id,@WH,@Now FROM dbo.Ingredient i
WHERE i.IngredientCode LIKE 'NL%' AND NOT EXISTS(SELECT 1 FROM dbo.StockLevel s WHERE s.IngredientId=i.Id AND s.WarehouseId=@WH);

-- Notification statuses
IF NOT EXISTS(SELECT 1 FROM dbo.NotificationStatus WHERE Name=N'Chưa đọc')
 INSERT dbo.NotificationStatus(Active,Name,Description,CreatedTime) VALUES(1,N'Chưa đọc',N'Thông báo mới',@Now);
IF NOT EXISTS(SELECT 1 FROM dbo.NotificationStatus WHERE Name=N'Đã đọc')
 INSERT dbo.NotificationStatus(Active,Name,Description,CreatedTime) VALUES(1,N'Đã đọc',N'Thông báo đã xem',@Now);

-- Financial target
IF NOT EXISTS(SELECT 1 FROM dbo.FinancialTarget WHERE Period='MONTHLY' AND YEAR(StartDate)=YEAR(@Now) AND MONTH(StartDate)=MONTH(@Now))
 INSERT dbo.FinancialTarget(TargetRevenue,TargetProfit,Period,StartDate,EndDate,CreatedTime,Active)
 VALUES(150000000,45000000,'MONTHLY',DATEFROMPARTS(YEAR(@Now),MONTH(@Now),1),EOMONTH(@Now),@Now,1);

COMMIT;
PRINT N'SEED DEMO THÀNH CÔNG';
SELECT 'DishCategory' [Table],COUNT(*) [Rows] FROM dbo.DishCategory
UNION ALL SELECT 'Dish',COUNT(*) FROM dbo.Dish
UNION ALL SELECT 'DishOrderStatus',COUNT(*) FROM dbo.DishOrderStatus
UNION ALL SELECT 'Supplier',COUNT(*) FROM dbo.Supplier
UNION ALL SELECT 'IngredientCategory',COUNT(*) FROM dbo.IngredientCategory
UNION ALL SELECT 'Ingredient',COUNT(*) FROM dbo.Ingredient
UNION ALL SELECT 'Unit',COUNT(*) FROM dbo.Unit
UNION ALL SELECT 'Warehouse',COUNT(*) FROM dbo.Warehouse
UNION ALL SELECT 'StockLevel',COUNT(*) FROM dbo.StockLevel;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 SELECT ERROR_NUMBER() ErrorNumber,ERROR_LINE() ErrorLine,ERROR_MESSAGE() ErrorMessage;
 THROW;
END CATCH;
