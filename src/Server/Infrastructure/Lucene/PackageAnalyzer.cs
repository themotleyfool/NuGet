using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Version = Lucene.Net.Util.Version;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class PackageAnalyzer : PerFieldAnalyzerWrapper
    {
        public static readonly Version IndexVersion = Version.LUCENE_29;

        public PackageAnalyzer() : base(new LowercaseKeywordAnalyzer())
        {
            base.AddAnalyzer("Text", new PorterStemAnalyzer(IndexVersion));
        }

        class PorterStemAnalyzer : StandardAnalyzer
        {
            public PorterStemAnalyzer(Version version) : base(version)
            {
            }

            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                return new PorterStemFilter(base.TokenStream(fieldName, reader));
            }
        }

        class LowercaseKeywordAnalyzer : KeywordAnalyzer
        {
            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
                return new LowerCaseFilter(base.TokenStream(fieldName, reader));
            }
        }
    }
}