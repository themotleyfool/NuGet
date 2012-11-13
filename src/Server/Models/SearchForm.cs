namespace NuGet.Server.Models
{
    public interface ISearchForm
    {
        string Query { get; set; }
        bool IncludePrerelease { get; set; }
        int PageSize { get; set; }
    }

    public class SearchForm : ISearchForm
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