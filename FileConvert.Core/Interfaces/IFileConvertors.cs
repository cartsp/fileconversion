using FileConvert.Core.Entities;
using System.Collections.Immutable;

namespace FileConvert.Core
{
    public interface IFileConvertors
    {
        IImmutableList<ConvertorDetails> GetCompatibleExtensions();

        IImmutableList<ConvertorDetails> GetAvailableConversions(string FileNameToConvert);
    }
}
