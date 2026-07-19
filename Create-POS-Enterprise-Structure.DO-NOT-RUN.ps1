param(
    [string]$Root = "POS_Enterprise_DotNet"
)

$ErrorActionPreference = "Stop"

function New-TextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Content = ""
    )

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    if (-not (Test-Path $Path)) {
        Set-Content -Path $Path -Value $Content -Encoding UTF8
        Write-Host "Created: $Path"
    }
    else {
        Write-Host "Exists:  $Path"
    }
}

function Set-TextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Content = ""
    )

    $directory = Split-Path -Parent $Path
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8
    Write-Host "Updated: $Path"
}

function New-PlaceholderCs {
    param([Parameter(Mandatory = $true)][string]$Path)

    New-TextFile -Path $Path -Content @"
// TODO: Paste the reviewed implementation for this file here.
"@
}

Write-Host "Checking .NET SDK..."
dotnet --version | Out-Host

$rootPath = Join-Path (Get-Location) $Root
New-Item -ItemType Directory -Path $rootPath -Force | Out-Null
Set-Location $rootPath

if (-not (Test-Path "POS.Enterprise.sln")) {
    dotnet new sln -n "POS.Enterprise"
}

if (-not (Test-Path "src/POS.Domain/POS.Domain.csproj")) {
    dotnet new classlib -n "POS.Domain" -o "src/POS.Domain" -f "net10.0"
}

if (-not (Test-Path "src/POS.Application/POS.Application.csproj")) {
    dotnet new classlib -n "POS.Application" -o "src/POS.Application" -f "net10.0"
}

if (-not (Test-Path "src/POS.Infrastructure/POS.Infrastructure.csproj")) {
    dotnet new classlib -n "POS.Infrastructure" -o "src/POS.Infrastructure" -f "net10.0"
}

if (-not (Test-Path "src/POS.Wpf/POS.Wpf.csproj")) {
    dotnet new wpf -n "POS.Wpf" -o "src/POS.Wpf" -f "net10.0"
}

dotnet sln "POS.Enterprise.sln" add `
    "src/POS.Domain/POS.Domain.csproj" `
    "src/POS.Application/POS.Application.csproj" `
    "src/POS.Infrastructure/POS.Infrastructure.csproj" `
    "src/POS.Wpf/POS.Wpf.csproj"

Remove-Item "src/POS.Domain/Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "src/POS.Application/Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "src/POS.Infrastructure/Class1.cs" -ErrorAction SilentlyContinue
Remove-Item "src/POS.Wpf/MainWindow.xaml" -ErrorAction SilentlyContinue
Remove-Item "src/POS.Wpf/MainWindow.xaml.cs" -ErrorAction SilentlyContinue

Set-TextFile "Directory.Build.props" @'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <Deterministic>true</Deterministic>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
'@

Set-TextFile "Directory.Packages.props" @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.0" />
    <PackageVersion Include="QRCoder" Version="1.7.0" />
  </ItemGroup>
</Project>
'@

Set-TextFile ".gitignore" @'
.vs/
bin/
obj/
*.user
*.suo
*.db
*.db-shm
*.db-wal
logs/
src/POS.Wpf/AppData/
'@

Set-TextFile "README.md" @'
# POS Enterprise

Ứng dụng POS Enterprise viết bằng C#, .NET 10, WPF và SQLite.
'@

Set-TextFile "ARCHITECTURE.md" @'
# Architecture

- POS.Domain: thực thể và luật nghiệp vụ.
- POS.Application: DTO, interface và dịch vụ ứng dụng.
- POS.Infrastructure: EF Core, SQLite, in hóa đơn và dịch vụ kỹ thuật.
- POS.Wpf: giao diện desktop WPF.
'@

Set-TextFile "Jenkinsfile" @'
// CI pipeline will be added later.
'@

New-TextFile "data/.gitkeep"
New-TextFile "tests/.gitkeep"
New-TextFile "src/POS.Infrastructure/Persistence/Migrations/.gitkeep"

Set-TextFile "src/POS.Domain/POS.Domain.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>POS.Domain</RootNamespace>
    <AssemblyName>POS.Domain</AssemblyName>
  </PropertyGroup>
</Project>
'@

Set-TextFile "src/POS.Application/POS.Application.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>POS.Application</RootNamespace>
    <AssemblyName>POS.Application</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\POS.Domain\POS.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
</Project>
'@

Set-TextFile "src/POS.Infrastructure/POS.Infrastructure.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RootNamespace>POS.Infrastructure</RootNamespace>
    <AssemblyName>POS.Infrastructure</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\POS.Domain\POS.Domain.csproj" />
    <ProjectReference Include="..\POS.Application\POS.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
    <PackageReference Include="QRCoder" />
  </ItemGroup>
</Project>
'@

Set-TextFile "src/POS.Wpf/POS.Wpf.csproj" @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RootNamespace>POS.Wpf</RootNamespace>
    <AssemblyName>POS.Enterprise</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\POS.Application\POS.Application.csproj" />
    <ProjectReference Include="..\POS.Infrastructure\POS.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
'@

$domainFiles = @(
    "Common/Entity.cs",
    "Common/AuditableEntity.cs",
    "Common/DomainException.cs",
    "Constants/BusinessRules.cs",
    "Enums/Role.cs",
    "Enums/CustomerTier.cs",
    "Enums/TableStatus.cs",
    "Enums/OrderStatus.cs",
    "Enums/OrderItemStatus.cs",
    "Enums/DiscountType.cs",
    "Enums/PaymentMethod.cs",
    "Enums/SyncStatus.cs",
    "Entities/User.cs",
    "Entities/Customer.cs",
    "Entities/Area.cs",
    "Entities/RestaurantTable.cs",
    "Entities/Category.cs",
    "Entities/Product.cs",
    "Entities/ModifierGroup.cs",
    "Entities/Modifier.cs",
    "Entities/Discount.cs",
    "Entities/Order.cs",
    "Entities/OrderItem.cs",
    "Entities/OrderItemModifier.cs",
    "Entities/OutboxMessage.cs"
)

foreach ($file in $domainFiles) {
    New-PlaceholderCs (Join-Path "src/POS.Domain" $file)
}

$appFiles = @(
    "Common/Error.cs",
    "Common/ErrorCodes.cs",
    "Common/Result.cs",
    "Common/ResultOfT.cs",
    "Common/PagedResult.cs",
    "Abstractions/Authentication/IPasswordHasher.cs",
    "Abstractions/Authentication/ICurrentUserService.cs",
    "Abstractions/Authentication/IAuthService.cs",
    "Abstractions/DateTime/IClock.cs",
    "Abstractions/Orders/IOrderCodeGenerator.cs",
    "Abstractions/Payments/IVietQrService.cs",
    "Abstractions/Printing/IReceiptService.cs",
    "Abstractions/Serialization/IJsonSerializer.cs",
    "Abstractions/Persistence/IApplicationTransaction.cs",
    "Abstractions/Persistence/IUnitOfWork.cs",
    "Abstractions/Persistence/IUserRepository.cs",
    "Abstractions/Persistence/IProductRepository.cs",
    "Abstractions/Persistence/ICategoryRepository.cs",
    "Abstractions/Persistence/ICustomerRepository.cs",
    "Abstractions/Persistence/IDiscountRepository.cs",
    "Abstractions/Persistence/IOrderRepository.cs",
    "Abstractions/Persistence/IOutboxRepository.cs",
    "Abstractions/Services/IProductService.cs",
    "Abstractions/Services/ICustomerService.cs",
    "Abstractions/Services/IDiscountService.cs",
    "Abstractions/Services/ICheckoutService.cs",
    "DTOs/Authentication/LoginRequest.cs",
    "DTOs/Authentication/AuthenticatedUserDto.cs",
    "DTOs/Products/ProductListItemDto.cs",
    "DTOs/Products/ProductDetailsDto.cs",
    "DTOs/Products/ProductSearchRequest.cs",
    "DTOs/Products/CreateProductRequest.cs",
    "DTOs/Products/UpdateProductRequest.cs",
    "DTOs/Customers/CustomerListItemDto.cs",
    "DTOs/Customers/CustomerDetailsDto.cs",
    "DTOs/Customers/CustomerSearchRequest.cs",
    "DTOs/Customers/CreateCustomerRequest.cs",
    "DTOs/Customers/UpdateCustomerRequest.cs",
    "DTOs/Discounts/DiscountListItemDto.cs",
    "DTOs/Discounts/DiscountDetailsDto.cs",
    "DTOs/Discounts/DiscountSearchRequest.cs",
    "DTOs/Discounts/CreateDiscountRequest.cs",
    "DTOs/Checkout/CheckoutLineRequest.cs",
    "DTOs/Checkout/CheckoutRequest.cs",
    "DTOs/Checkout/CheckoutLineResultDto.cs",
    "DTOs/Checkout/CheckoutResultDto.cs",
    "DTOs/Payments/VietQrRequest.cs",
    "DTOs/Printing/ReceiptLineDto.cs",
    "DTOs/Printing/ReceiptRequest.cs",
    "Factories/OutboxEventFactory.cs",
    "Validation/PhoneNumberNormalizer.cs",
    "Validation/CheckoutValidator.cs",
    "Services/AuthService.cs",
    "Services/ProductService.cs",
    "Services/CustomerService.cs",
    "Services/DiscountService.cs",
    "Services/CheckoutService.cs"
)

foreach ($file in $appFiles) {
    New-PlaceholderCs (Join-Path "src/POS.Application" $file)
}

$infraFiles = @(
    "InfrastructureOptions.cs",
    "DependencyInjection.cs",
    "Authentication/BCryptPasswordHasher.cs",
    "Authentication/CurrentUserService.cs",
    "Common/SystemClock.cs",
    "Common/SystemTextJsonSerializer.cs",
    "Orders/OrderCodeGenerator.cs",
    "Payments/VietQrService.cs",
    "Printing/ReceiptDocumentBuilder.cs",
    "Printing/WpfReceiptService.cs",
    "Persistence/PosDbContext.cs",
    "Persistence/DesignTimePosDbContextFactory.cs",
    "Persistence/AuditableEntityInterceptor.cs",
    "Persistence/EfUnitOfWork.cs",
    "Persistence/EfApplicationTransaction.cs",
    "Persistence/DatabaseInitializer.cs",
    "Persistence/DatabasePathResolver.cs",
    "Persistence/Configurations/UserConfiguration.cs",
    "Persistence/Configurations/CustomerConfiguration.cs",
    "Persistence/Configurations/AreaConfiguration.cs",
    "Persistence/Configurations/RestaurantTableConfiguration.cs",
    "Persistence/Configurations/CategoryConfiguration.cs",
    "Persistence/Configurations/ProductConfiguration.cs",
    "Persistence/Configurations/ModifierGroupConfiguration.cs",
    "Persistence/Configurations/ModifierConfiguration.cs",
    "Persistence/Configurations/DiscountConfiguration.cs",
    "Persistence/Configurations/OrderConfiguration.cs",
    "Persistence/Configurations/OrderItemConfiguration.cs",
    "Persistence/Configurations/OrderItemModifierConfiguration.cs",
    "Persistence/Configurations/OutboxMessageConfiguration.cs",
    "Persistence/Repositories/UserRepository.cs",
    "Persistence/Repositories/ProductRepository.cs",
    "Persistence/Repositories/CategoryRepository.cs",
    "Persistence/Repositories/CustomerRepository.cs",
    "Persistence/Repositories/DiscountRepository.cs",
    "Persistence/Repositories/OrderRepository.cs",
    "Persistence/Repositories/OutboxRepository.cs"
)

foreach ($file in $infraFiles) {
    New-PlaceholderCs (Join-Path "src/POS.Infrastructure" $file)
}

$wpfCsFiles = @(
    "Commands/RelayCommand.cs",
    "Commands/AsyncRelayCommand.cs",
    "Converters/BooleanToVisibilityConverter.cs",
    "Services/DialogService.cs",
    "Services/WindowService.cs",
    "ViewModels/ViewModelBase.cs",
    "ViewModels/LoginViewModel.cs",
    "ViewModels/ShellViewModel.cs"
)

foreach ($file in $wpfCsFiles) {
    New-PlaceholderCs (Join-Path "src/POS.Wpf" $file)
}

Set-TextFile "src/POS.Wpf/Themes/Colors.xaml" @'
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</ResourceDictionary>
'@

Set-TextFile "src/POS.Wpf/Themes/Typography.xaml" @'
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</ResourceDictionary>
'@

Set-TextFile "src/POS.Wpf/Themes/Controls.xaml" @'
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</ResourceDictionary>
'@

Set-TextFile "src/POS.Wpf/Views/LoginWindow.xaml" @'
<Window x:Class="POS.Wpf.Views.LoginWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="POS Enterprise - Đăng nhập"
        Width="1100"
        Height="700"
        WindowStartupLocation="CenterScreen">
    <Grid />
</Window>
'@

Set-TextFile "src/POS.Wpf/Views/LoginWindow.xaml.cs" @'
using System.Windows;

namespace POS.Wpf.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }
}
'@

Set-TextFile "src/POS.Wpf/Views/ShellWindow.xaml" @'
<Window x:Class="POS.Wpf.Views.ShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="POS Enterprise"
        Width="1400"
        Height="900"
        WindowStartupLocation="CenterScreen">
    <Grid />
</Window>
'@

Set-TextFile "src/POS.Wpf/Views/ShellWindow.xaml.cs" @'
using System.Windows;

namespace POS.Wpf.Views;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }
}
'@

Set-TextFile "src/POS.Wpf/App.xaml" @'
<Application x:Class="POS.Wpf.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/LoginWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Colors.xaml" />
                <ResourceDictionary Source="Themes/Typography.xaml" />
                <ResourceDictionary Source="Themes/Controls.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
'@

Set-TextFile "src/POS.Wpf/App.xaml.cs" @'
using System.Windows;

namespace POS.Wpf;

public partial class App : Application
{
}
'@

Set-TextFile "src/POS.Wpf/appsettings.json" @'
{
  "Infrastructure": {
    "DatabasePath": "data/pos-enterprise.db",
    "DatabaseTimeoutSeconds": 30,
    "ApplyMigrationsOnStartup": true,
    "SeedDefaultAdministrator": true,
    "DefaultAdminUsername": "admin",
    "DefaultAdminPassword": "admin123",
    "DefaultAdminFullName": "Quản trị viên hệ thống"
  },
  "Store": {
    "Name": "POS ENTERPRISE",
    "Address": "Địa chỉ cửa hàng",
    "Phone": "0999 888 777",
    "WifiPassword": ""
  },
  "Hardware": {
    "PrinterName": "Microsoft Print to PDF",
    "PaperSize": "K80"
  },
  "Payment": {
    "BaseVietQrPayload": "",
    "DisplayQrOnReceipt": true
  }
}
'@

Set-TextFile "src/POS.Wpf/app.manifest" @'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0"
          xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="POS.Enterprise" />
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
'@

Write-Host ""
Write-Host "Restoring NuGet packages..."
dotnet restore "POS.Enterprise.sln"

Write-Host ""
Write-Host "Building solution..."
dotnet build "POS.Enterprise.sln"

Write-Host ""
Write-Host "======================================================"
Write-Host "Scaffold completed at:"
Write-Host $rootPath
Write-Host "======================================================"
Write-Host "Run the WPF project with:"
Write-Host 'dotnet run --project "src/POS.Wpf/POS.Wpf.csproj"'
