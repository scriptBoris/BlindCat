using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Windows.Input;
using ValidatorSam;

namespace BlindCatCore.ViewModels;

public class StorageCreateVm : BaseVm<StorageDir>
{
    private readonly IViewPlatforms _viewPlatforms;
    private readonly IStorageService _storageService;
    private readonly bool _useAutoInit;
    public class Key : IKey<StorageCreateVm>
    {
        /// <summary>
        /// Укзаывает на то, надо ли сразу после создания хранилища, сразу 
        /// его инициализировать (открывать)?
        /// </summary>
        public bool UseAutoInit {  get; init; }
    }

    public StorageCreateVm(Key key, IViewPlatforms viewPlatforms, IStorageService storageService)
    {
        _viewPlatforms = viewPlatforms;
        _storageService = storageService;
        _useAutoInit = key.UseAutoInit;
    }

    public Validator<string?> Name => Validator<string?>.Build()
        .UsingTextLimit(3, 20)
        .UsingRequired();

    public Validator<string?> Path => Validator<string?>.Build()
        .UsingSafeRule(x => !global::System.IO.Directory.Exists(x), "No exist path")
        .UsingRequired();

    public Validator<string?> Password => Validator<string?>.Build()
        .UsingTextLimit(8, 30)
        .UsingRequired();

    public ICommand CommandCreate => new Cmd(async () =>
    {
        var res = Validator.GetAll(this).FirstInvalidOrDefault();
        if (res != null)
        {
            await ShowError($"Ошибка данных\n{res.Value.TextError}");
            return;
        }

        var store = new StorageDir
        {
            Guid = Guid.NewGuid(),
            Name = Name.Value!,
            Path = Path.Value!,
        };
        var addRes = await _storageService.AddStorage(store, Password);
        if (addRes.IsFault)
        {
            await HandleError(addRes);
            return;
        }

        if (_useAutoInit)
        {
            store.Controller = new StorageDirController
            {
                Storage = store,
                Password = Password!,
            };
        }

        await SetResultAndPop(store);
    });
}
