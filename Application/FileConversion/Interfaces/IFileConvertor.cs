using FileConvert.Core.Entities;
using System.Collections.Immutable;

namespace Application.FileConversion.Interfaces
{
    public interface IFileConvertor
    {
        IImmutableList<ConvertorDetails> GetAllAvailableConvertors();

        IImmutableList<ConvertorDetails> GetConvertorsForFile(string FileNameToConvert);
    }
}
