using System.Text;
using System.Xml;
using HtmlAgilityPack;
using VersOne.Epub;

namespace EpubParser
{
    public static class ExtractPlainText
    {
        public static IEnumerable<Tuple<string, string>> Run(string filePath)
        {
            EpubBook book = EpubReader.ReadBook(filePath);
            return Walk(book);
        }

        private static Tuple<string, string> PrintTextContentFile(EpubLocalTextContentFile textContentFile, EpubNavigationItem navigationItem, int count)
        {
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(textContentFile.Content);
            StringBuilder sb = new();
            foreach (HtmlNode node in htmlDocument.DocumentNode.SelectNodes("//text()"))
            {
                sb.AppendLine(node.InnerText.Trim());
            }
            
            string contentText = sb.ToString();
            return new Tuple<string, string>($"{count}_{navigationItem.Title}", contentText);
   
        }

        public static IEnumerable<Tuple<string, string>> Walk(EpubBook book)
        {
            List<EpubNavigationItem> navigations = book.Navigation.SelectMany(d=>d.Flatten()).ToList();
            int coutn = 0;
            foreach (var item in navigations)
            {
                if (ShoudSkipNavigationItem(item) == false)
                {
                    foreach (EpubLocalTextContentFile textContentFile in book.ReadingOrder.Where(s => s.FilePath == item?.Link?.ContentFilePath))
                    {
                        yield return PrintTextContentFile(textContentFile,item, coutn);
                        coutn++;

                    }

                }
            }
        }
       


        private static  bool ShoudSkipNavigationItem(EpubNavigationItem navigation)
        {
            return (TO_SKIP.Contains(navigation.Title));
        }

        internal static  string[] TO_SKIP = new[] 
        {
            "Cover",
            "Title Page",
            "Copyright",
            "Epigraph",
            "Contents",
            "Appendix",
            "Acknowledgments",
            "Notes",
            "Index",
            "About the Author",
        };
    }

    public static class EpubNavigationItemExtensions
    {
        public static List<EpubNavigationItem> Flatten(this EpubNavigationItem root)
        {
            var flatList = new List<EpubNavigationItem>();

            void FlattenRecursive(EpubNavigationItem item)
            {
                flatList.Add(item);
                foreach (var nestedItem in item.NestedItems)
                {
                    FlattenRecursive(nestedItem);
                }
            }

            FlattenRecursive(root);

            return flatList;
        }

        public static List<FlattenedEpubNavigationItem> FlattenWithLevels(this EpubNavigationItem root, int mainLevel = 0)
        {
            var flatList = new List<FlattenedEpubNavigationItem>();

            void FlattenRecursive(EpubNavigationItem item, int nestingLevel)
            {
                flatList.Add(new FlattenedEpubNavigationItem(item, mainLevel, nestingLevel));
             
                foreach (var nestedItem in item.NestedItems)
                {
                    FlattenRecursive(nestedItem, nestingLevel + 1);
                }
            }

            FlattenRecursive(root, mainLevel);

            return flatList;
        }
    }

    public class FlattenedEpubNavigationItem
    {
        public EpubNavigationItem Item { get; }
        public int MainLevel { get; }
        public int NestingLevel { get; }

        public FlattenedEpubNavigationItem(EpubNavigationItem item, int mainLevel, int nestingLevel)
        {
            Item = item;
            MainLevel = mainLevel;
            NestingLevel = nestingLevel;
        }
    }
}