using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using FileConvert.Core.Entities;
using System.Linq;

namespace FileConvert.Infrastructure
{
    public static class ConversionDetailsExtensions
    {
        /// <summary>
        /// Filters the conversions available by the FromExtension
        /// </summary>
        /// <param name="FromExtension">Find convertors for this extension</param>
        /// <returns>Convertors for this extension</returns>
        public static IImmutableList<ConvertorDetails> ThatConvertFrom
            (this IImmutableList<ConvertorDetails> CurrentList, string FromExtension)
        {
            return CurrentList.Where(converter => converter.ExtensionToConvert == FromExtension).ToImmutableList();
        }

        /// <summary>
        /// Filters the conversions available by the ToExtension
        /// </summary>
        /// <param name="ToExtension">Find convertors for this extension</param>
        /// <returns>Convertors for this extension</returns>
        public static IImmutableList<ConvertorDetails> ThatConvertTo
            (this IImmutableList<ConvertorDetails> CurrentList, string ToExtension)
        {
            return CurrentList.Where(converter => converter.ConvertedExtension == ToExtension).ToImmutableList();
        }
    }    
}
