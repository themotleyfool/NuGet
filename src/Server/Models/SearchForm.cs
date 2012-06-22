namespace NuGet.Server.Models
{
    public class SearchForm
    {
        public string Query { get; set; }
        public bool IncludePrerelease { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public SearchForm()
        {
            PageSize = DefaultPageSize;
        }

        public static int DefaultPageSize
        {
            get { return 20; }
        }
    }
}