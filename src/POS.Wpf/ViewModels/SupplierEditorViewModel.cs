using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Suppliers;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed class SupplierEditorViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private int? _supplierId;
    private DateTimeOffset _expectedUpdatedAtUtc;
    private string _code = string.Empty;
    private string _name = string.Empty;
    private string _taxCode = string.Empty;
    private string _contactName = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _emailAddress = string.Empty;
    private string _address = string.Empty;
    private string _notes = string.Empty;
    private string _initialFingerprint = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _isBusy;

    public SupplierEditorViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy, HandleCommandException);
        CancelCommand = new AsyncRelayCommand(() => { RequestClose?.Invoke(false); return Task.CompletedTask; });
    }

    public event Action<bool?>? RequestClose;
    public int? SavedSupplierId { get; private set; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public bool IsCreateMode => !_supplierId.HasValue;
    public string Title => IsCreateMode ? "Thêm nhà cung cấp" : "Chỉnh sửa nhà cung cấp";
    public string Code { get => _code; set { if (SetProperty(ref _code, value ?? string.Empty)) NotifyDirty(); } }
    public string Name { get => _name; set { if (SetProperty(ref _name, value ?? string.Empty)) NotifyDirty(); } }
    public string TaxCode { get => _taxCode; set { if (SetProperty(ref _taxCode, value ?? string.Empty)) NotifyDirty(); } }
    public string ContactName { get => _contactName; set { if (SetProperty(ref _contactName, value ?? string.Empty)) NotifyDirty(); } }
    public string PhoneNumber { get => _phoneNumber; set { if (SetProperty(ref _phoneNumber, value ?? string.Empty)) NotifyDirty(); } }
    public string EmailAddress { get => _emailAddress; set { if (SetProperty(ref _emailAddress, value ?? string.Empty)) NotifyDirty(); } }
    public string Address { get => _address; set { if (SetProperty(ref _address, value ?? string.Empty)) NotifyDirty(); } }
    public string Notes { get => _notes; set { if (SetProperty(ref _notes, value ?? string.Empty)) NotifyDirty(); } }
    public string ValidationMessage { get => _validationMessage; private set { if (SetProperty(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) SaveCommand.NotifyCanExecuteChanged(); } }
    public bool IsDirty => Fingerprint() != _initialFingerprint;

    public async Task InitializeAsync(int? supplierId)
    {
        _supplierId = supplierId;
        if (supplierId.HasValue)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISupplierService>().GetByIdAsync(supplierId.Value);
            if (result.IsFailure) { ValidationMessage = result.AppError.Message; return; }
            Apply(result.Value);
        }
        _initialFingerprint = Fingerprint();
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsDirty));
    }

    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        IsBusy = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ISupplierService>();
            var result = IsCreateMode
                ? await service.CreateAsync(new CreateSupplierRequest(Code, Name, TaxCode, ContactName, PhoneNumber, EmailAddress, Address, Notes))
                : await service.UpdateAsync(new UpdateSupplierRequest(_supplierId!.Value, Code, Name, TaxCode, ContactName, PhoneNumber, EmailAddress, Address, Notes, _expectedUpdatedAtUtc));
            if (result.IsFailure) { ValidationMessage = result.AppError.Message; return; }
            SavedSupplierId = result.Value.Id;
            _initialFingerprint = Fingerprint();
            OnPropertyChanged(nameof(IsDirty));
            RequestClose?.Invoke(true);
        }
        finally { IsBusy = false; }
    }

    private void Apply(SupplierDetailsDto value)
    {
        SavedSupplierId = value.Id;
        _expectedUpdatedAtUtc = value.UpdatedAtUtc;
        Code = value.Code; Name = value.Name; TaxCode = value.TaxCode ?? string.Empty;
        ContactName = value.ContactName ?? string.Empty; PhoneNumber = value.PhoneNumber ?? string.Empty;
        EmailAddress = value.EmailAddress ?? string.Empty; Address = value.Address ?? string.Empty; Notes = value.Notes ?? string.Empty;
    }

    private string Fingerprint() => string.Join("\u001F", Code, Name, TaxCode, ContactName, PhoneNumber, EmailAddress, Address, Notes);
    private void NotifyDirty() => OnPropertyChanged(nameof(IsDirty));
    private static void HandleCommandException(Exception exception) { }
}
