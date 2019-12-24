using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace FileConvert
{
    public static class FileUtil
    {
        public static async Task SaveAs(this IJSRuntime js, string filename, byte[] data)
            => await js.InvokeAsync<object>(
                "saveAsFile",
                filename,
                Convert.ToBase64String(data));
    }
}
