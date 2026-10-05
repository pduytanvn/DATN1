# CoffeeCRM - He thong quan ly quan ca phe

## 1. Gioi thieu

CoffeeCRM la he thong quan ly quan ca phe duoc xay dung bang ASP.NET Core va SQL Server, ho tro quan ly cac nghiep vu chinh trong qua trinh van hanh cua hang.

Cac chuc nang chinh:

- Quan ly ban va dat ban
- Quan ly thuc don va danh muc mon
- Goi mon va xu ly don hang
- Quan ly hoa don va thanh toan
- Quan ly nguyen lieu va kho
- Quan ly nhap - xuat kho
- Quan ly nha cung cap va cong no
- Quan ly nhan vien va cham cong
- Quan ly thu - chi
- Theo doi doanh thu va hoat dong kinh doanh

## 2. Phan quyen

He thong gom 4 nhom quyen chinh:

- ADMIN - Quan tri vien: quan ly va su dung cac chuc nang quan tri he thong
- MANAGER - Quan ly: theo doi hoat dong kinh doanh, nhan su, kho va cac nghiep vu quan ly
- BARTENDER - Nhan vien pha che: tiep nhan va xu ly cac don goi mon
- WAITER - Nhan vien phuc vu: quan ly ban, dat ban, goi mon va phuc vu khach hang

## 3. Cong nghe su dung

- ASP.NET Core
- .NET 6
- Entity Framework Core
- SQL Server
- ASP.NET Core MVC
- REST API
- HTML, CSS, JavaScript, Bootstrap, jQuery
- Docker

## 4. Cau truc project

```text
CfCRM.DATN
├── CoffeeCRM.Api
├── CoffeeCRM.Core
├── CoffeeCRM.Data
└── CoffeeCRM.View
```

- `CoffeeCRM.Api` - cung cap cac API cua he thong
- `CoffeeCRM.Core` - xu ly nghiep vu va service
- `CoffeeCRM.Data` - Model, DTO, DbContext, Migration va du lieu
- `CoffeeCRM.View` - giao dien ASP.NET Core MVC

## 5. Database

Database su dung:

```text
Cf.CRM.NCKH.v02
```

File du lieu mau:

```text
PhamDuyTan_2021605870.sql
```

## 6. Huong dan khoi chay

### Buoc 1 - Khoi dong SQL Server

Neu su dung SQL Server bang Docker:

```bash
docker start coffee-sql
```

Kiem tra:

```bash
docker ps
```

### Buoc 2 - Khoi dong API

Tai thu muc project:

```bash
cd ~/Projects/CfCRM.DATN
dotnet run --project CoffeeCRM.Api --urls http://localhost:7273
```

API chay tai:

```text
http://localhost:7273
```

### Buoc 3 - Khoi dong giao dien

Mo Terminal moi:

```bash
cd ~/Projects/CfCRM.DATN
dotnet run --project CoffeeCRM.View --urls http://localhost:5027
```

Truy cap:

```text
http://localhost:5027
```

## 7. Tai khoan dang nhap

Tai khoan quan tri mac dinh:

```text
Username: admin
Password: 123456
```

## 8. Luu y

- SQL Server can duoc khoi dong truoc khi chay API
- API can hoat dong de giao dien lay duoc du lieu
- Khong chay dong thoi CoffeeCRM.View bang Terminal va IDE tren cung port 5027
- Khi thay doi thong tin SQL Server can cap nhat lai Connection String
- File `PhamDuyTan_2021605870.sql` duoc su dung de bo sung du lieu mau cho he thong