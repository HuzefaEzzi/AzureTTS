// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Xml.Linq;
using EpubParser;

namespace TTSSample
{
    public class Authentication
    {
        private string subscriptionKey;
        private string tokenFetchUri;

        public Authentication(string tokenFetchUri, string subscriptionKey)
        {
            if (string.IsNullOrWhiteSpace(tokenFetchUri))
            {
                throw new ArgumentNullException(nameof(tokenFetchUri));
            }
            if (string.IsNullOrWhiteSpace(subscriptionKey))
            {
                throw new ArgumentNullException(nameof(subscriptionKey));
            }
            this.tokenFetchUri = tokenFetchUri;
            this.subscriptionKey = subscriptionKey;
        }

        public async Task<string> FetchTokenAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.subscriptionKey);
                UriBuilder uriBuilder = new UriBuilder(this.tokenFetchUri);

                HttpResponseMessage result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null).ConfigureAwait(false);
                return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
    class Program
    {
        static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        private static string outputPath = @"";
        private static string inputPath = @"";

        static async Task Main(string[] args)
        {
            // Prompts the user to input text for TTS conversion
            //Console.Write("What would you like to convert to speech? ");

            // Gets an access token
            Console.Write("Please enter the file path");
            string parsingFile = Console.ReadLine();

            string parsingBasePath = Path.GetDirectoryName(parsingFile);


            string parsedFilesDir = Path.Combine(parsingBasePath, "Parsed");
            Console.Write($"Creatign Parsed dir at {parsedFilesDir}");

            Directory.CreateDirectory(parsedFilesDir);


            IEnumerable<Tuple<string, string>> contents = ExtractPlainText.Run(parsingFile);

            int count = 0;
            foreach (var item in contents)
            {

                File.WriteAllText(@$"{parsedFilesDir}\{MakeValidFileName(item.Item1)}.txt", item.Item2);
                count++;

            }
            Console.Write($"File PArsed, please check at {parsedFilesDir} press enter");
            Console.ReadLine();
            inputPath = parsedFilesDir;
            outputPath = Path.Combine(parsingBasePath, "output");

            Directory.CreateDirectory(outputPath);


            string accessToken;
            Console.WriteLine("Attempting token exchange. Please wait...\n");

            // Add your subscription key here
            // If your resource isn't in WEST US, change the endpoint
            Authentication auth = new Authentication("https://eastus.api.cognitive.microsoft.com/sts/v1.0/issueToken", Environment.GetEnvironmentVariable("AZURE_CONGENTIVE_KEY"));
            try
            {
                accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Console.WriteLine("Successfully obtained an access token. \n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to obtain an access token.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return;
            }
            await Parallel.ForEachAsync(Directory.EnumerateFiles(inputPath), async (file, token) =>
            {
                try
                {
                    await Convert(accessToken, file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting file:{file}", ex);
                }
            });



        }

        private static async Task Convert(string accessToken, string inputPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            if (File.Exists(Path.Combine(outputPath, fileName+".wav")))
            {
                Console.WriteLine($"Already present '{fileName}' skipping ");
                return;
            }
            Console.WriteLine($"Processing {inputPath} ");
            string text = File.ReadAllText(inputPath);
            string host = "https://eastus.tts.speech.microsoft.com/cognitiveservices/v1";

            // Create SSML document.
            XDocument body = new XDocument(
                    new XElement("speak",
                        new XAttribute("version", "1.0"),
                        new XAttribute(XNamespace.Xml + "lang", "en-US"),
                        new XElement("voice",
                            new XAttribute(XNamespace.Xml + "lang", "en-US"),
                            new XAttribute(XNamespace.Xml + "gender", "Male"),
                            new XAttribute("name", "en-US-DavisNeural"), // Short name for "Microsoft Server Speech Text to Speech Voice (en-US, Jessa24KRUS)"
                            text)));
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    // Set the HTTP method
                    request.Method = HttpMethod.Post;
                    // Construct the URI
                    request.RequestUri = new Uri(host);
                    // Set the content type header
                    request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/ssml+xml");
                    // Set additional header, such as Authorization and User-Agent
                    request.Headers.Add("Authorization", "Bearer " + accessToken);
                    request.Headers.Add("Connection", "Keep-Alive");
                    // Update your resource name
                    request.Headers.Add("User-Agent", "huzefa-tts-test");
                    // Audio output format. See API reference for full list.
                    request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
                    // Create a request
                    Console.WriteLine($"Calling the TTS service{inputPath}. Please wait... \n");
                    using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        // Asynchronously read the response
                        using (Stream dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            Console.WriteLine("Your speech file is being written to file...");
                            using (FileStream fileStream = new(Path.Combine(outputPath, Path.GetFileNameWithoutExtension(inputPath) + ".wav"), FileMode.Create, FileAccess.Write, FileShare.Write))
                            {
                                await dataStream.CopyToAsync(fileStream).ConfigureAwait(false);
                                fileStream.Close();
                            }
                            Console.WriteLine($"\nYour file is ready {inputPath}. Press any key to exit.");
                        }
                    }
                }
            }
            Console.WriteLine($"Completed {inputPath} ");

        }
    }
}