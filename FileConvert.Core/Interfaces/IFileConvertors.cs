using FileConvert.Core.Entities;
using System.Collections.Immutable;

namespace FileConvert.Core
{
    public interface IFileConvertors
    {
        IImmutableList<ConvertorDetails> GetAllAvailableConvertors();

        IImmutableList<ConvertorDetails> GetConvertorsForFile(string FileNameToConvert);
    }
}
