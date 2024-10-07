using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Models
{
    public class GalPhoto
    {
        public string? PreviewPath {  get; set; }
        public required string PhotoPath { get; set; }
    }
}
