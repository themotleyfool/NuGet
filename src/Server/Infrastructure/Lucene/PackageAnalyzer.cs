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
            var porterStemAnalyzer = new PorterStemAnalyzer(IndexVersion);
            var standardAnalyzer = new StandardAnalyzer(IndexVersion);

            base.AddAnalyzer("Description", porterStemAnalyzer);
            base.AddAnalyzer("ReleaseNotes", porterStemAnalyzer);
            base.AddAnalyzer("Summary", porterStemAnalyzer);
            base.AddAnalyzer("Tags", porterStemAnalyzer);
            
            base.AddAnalyzer("Authors", standardAnalyzer);
            base.AddAnalyzer("Owners", standardAnalyzer);

            base.AddAnalyzer("Dependencies", new DependencyAnalyzer());

            base.AddAnalyzer("Path", new KeywordAnalyzer());
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

        class DependencyAnalyzer : LowercaseKeywordAnalyzer
        {
            public override TokenStream TokenStream(string fieldName, TextReader reader)
            {
            	var name = reader.ReadToEnd().Split(':')[0];
            	return base.TokenStream(fieldName, new StringReader(name));
            }
        }
    }
}