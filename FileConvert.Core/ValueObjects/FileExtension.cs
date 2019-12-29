namespace FileConvert.Core.ValueObjects
{
    public class FileExtension
    {
        private FileExtension(string value) { Value = value; }

        public string Value { get; set; }

        public static FileExtension gif { get { return new FileExtension(".gif"); } }
        public static FileExtension jpg { get { return new FileExtension(".jpg"); } }
        public static FileExtension jpeg { get { return new FileExtension(".jpeg"); } }
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
        public static FileExtension tif { get { return new FileExtension(".tif"); } }

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