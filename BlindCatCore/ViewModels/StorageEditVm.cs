using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System.Windows.Input;
using ValidatorSam;
using static BlindCatCore.ViewModels.StorageEditVm;

namespace BlindCatCore.ViewModels;

public class StorageEditVm : BaseVm<StorageEditVmResult>
{
    private readonly StorageDir _storageCell;
    private readonly IStorageService _storageService;

    public class Key : IKey<StorageEditVm>
    {
        public required StorageDir Storage { get; set; }
    }
    public StorageEditVm(Key key, IStorageService storageService)
    {
        _storageCell = key.Storage;
        _storageService = storageService;

        Name.SetValueAsRat(_storageCell.Name, RatModes.InitValue);
        Directory.SetValueAsRat(_storageCell.Path, RatModes.InitValue);
    }

    #region props
    public Validator<string> Name => Validator<string>.Build()
        .UsingRequired();

    public Validator<string> Directory => Validator<string>.Build()
        .UsingRequired();
    #endregion props

    #region commands
    public ICommand CommandSave => new Cmd(async () =>
    {
        bool isSuccess = Validator.GetAll(this).CheckSuccess();
        if (!isSuccess)
            return;

        _storageCell.Name = Name.Value;
        await Close();
    });

    public ICommand CommandDelete => new Cmd(async () =>
    {
        string? delPassword = await ShowDialogPromtPassword("Delete",
            "You are sure? All encrypted data, media, files into this storage was removed\n" +
            "Is you are use, typing password of this storage",
            "DELETE",
            "Cancel");

        if (delPassword == null)
            return;

        if (!await _storageService.CheckPasswordCorrect(_storageCell, delPassword))
        {
            await ShowError("Password is incorrect");
            return;
        }

        using var busy = Loading();
        var res = await _storageService.DeleteStorage(_storageCell);
        if (res.IsSuccess)
        {
            await ShowMessage("Deleted", "The Storage was deleted", "OK");
        }
        else
        {
            await ShowMessage("Deleted", res.Description, "OK");
        }

        await SetResultAndPop(new StorageEditVmResult
        {
            IsDeleted = true,
        });
    });
    #endregion commands

    public class StorageEditVmResult
    {
        public bool IsDeleted { get; set; }
    }
}