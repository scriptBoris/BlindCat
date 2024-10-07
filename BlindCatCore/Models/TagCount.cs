using BlindCatCore.Core;

namespace BlindCatCore.Models;

public class TagCount : BaseNotify
{
    public string TagName { get; set; }
    public int Count { get; set; }
}