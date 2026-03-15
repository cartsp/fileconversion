namespace FileConvert.Core.ValueObjects
{
    public class FileExtension
    {
        private FileExtension(string value) { Value = value; }

        public string Value { get; set; }

        public static FileExtension gif { get { return new FileExtension(".gif"); } }
        public static FileExtension jpg { get { return new FileExtension(".jpg"); } }
        public static FileExtension jpeg { get { return new FileExtension(".jpeg"); } }
        public static FileExtension jfif { get { return new FileExtension(".jfif"); } }
        public static FileExtension png { get { return new FileExtension(".png"); } }
        public static FileExtension bmp { get { return new FileExtension(".bmp"); } }
        public static FileExtension docx { get { return new FileExtension(".docx"); } }
        public static FileExtension pdf { get { return new FileExtension(".pdf"); } }
        public static FileExtension csv { get { return new FileExtension(".csv"); } }
        public static FileExtension xlsx { get { return new FileExtension(".xlsx"); } }
        public static FileExtension xls { get { return new FileExtension(".xls"); } }
        public static FileExtension mp3 { get { return new FileExtension(".mp3"); } }
        public static FileExtension wav { get { return new FileExtension(".wav"); } }
        public static FileExtension html { get { return new FileExtension(".html"); } }
        public static FileExtension md { get { return new FileExtension(".md"); } }
        public static FileExtension tif { get { return new FileExtension(".tif"); } }
        public static FileExtension json { get { return new FileExtension(".json"); } }
        public static FileExtension xml { get { return new FileExtension(".xml"); } }
        public static FileExtension yaml { get { return new FileExtension(".yaml"); } }
        public static FileExtension yml { get { return new FileExtension(".yml"); } }
        public static FileExtension tsv { get { return new FileExtension(".tsv"); } }
        public static FileExtension txt { get { return new FileExtension(".txt"); } }
        public static FileExtension webp { get { return new FileExtension(".webp"); } }
        public static FileExtension tiff { get { return new FileExtension(".tiff"); } }
        public static FileExtension ico { get { return new FileExtension(".ico"); } }
        public static FileExtension svg { get { return new FileExtension(".svg"); } }
        public static FileExtension zip { get { return new FileExtension(".zip"); } }
        public static FileExtension tar { get { return new FileExtension(".tar"); } }
        public static FileExtension gz { get { return new FileExtension(".gz"); } }
        public static FileExtension tgz { get { return new FileExtension(".tgz"); } }
        public static FileExtension bz2 { get { return new FileExtension(".bz2"); } }
        public static FileExtension tbz2 { get { return new FileExtension(".tbz2"); } }
        public static FileExtension qr { get { return new FileExtension(".qr"); } }
        public static FileExtension _7z { get { return new FileExtension(".7z"); } }
        public static FileExtension rar { get { return new FileExtension(".rar"); } }
        public static FileExtension jp2 { get { return new FileExtension(".jp2"); } }
        public static FileExtension j2k { get { return new FileExtension(".j2k"); } }
        public static FileExtension epub { get { return new FileExtension(".epub"); } }
        public static FileExtension heic { get { return new FileExtension(".heic"); } }
        public static FileExtension heif { get { return new FileExtension(".heif"); } }
        public static FileExtension avif { get { return new FileExtension(".avif"); } }
        public static FileExtension jxl { get { return new FileExtension(".jxl"); } }
        public static FileExtension dng { get { return new FileExtension(".dng"); } }
        public static FileExtension pptx { get { return new FileExtension(".pptx"); } }

        public static implicit operator string(FileExtension value)
        {
            return value.Value;
        }

        public static implicit operator FileExtension(string value)
        {
            return new FileExtension(value);
        }

        #region Equality

        public override bool Equals(object obj)
        {
            var other = obj as FileExtension;

            return other != null ? Equals(other) : Equals(obj as string);
        }

        public bool Equals(FileExtension other) => other != null && Value == other.Value;

        public bool Equals(string other) => Value == other;

        public static bool operator ==(FileExtension a, FileExtension b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (((object)a == null) || ((object)b == null)) return false;

            return a.Value == b.Value;
        }

        public static bool operator !=(FileExtension a, FileExtension b) => !(a == b);

        #endregion

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;
    }

}
