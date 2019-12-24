using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FileConvert.Core.Entities
{
    public class ConvertorDetails
    {
        public string ExtensionToConvert { get; set; }
        public string ConvertedExtension { get; set; }
        public Func<MemoryStream, Task<MemoryStream>> Convertor { get; set; }

        public ConvertorDetails(string ExtensionToConvert, string ConvertedExtension, Func<MemoryStream, Task<MemoryStream>> Convertor)
        {
            this.ExtensionToConvert = ExtensionToConvert;
            this.ConvertedExtension = ConvertedExtension;
            this.Convertor = Convertor;
        }
    }
}
