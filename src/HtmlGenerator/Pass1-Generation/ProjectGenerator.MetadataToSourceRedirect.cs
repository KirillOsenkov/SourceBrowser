using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        /// <summary>
        /// Since the only requirement on the ID strings we use in the a.html file
        /// is that there are no collisions (and even if there are, the failure
        /// would be rare and impact would be limited), we don't really need 16
        /// bytes per ID. Let's just store the first 8 bytes (I've actually calculated
        /// using MinimalUniquenessPreservingPrefixLength that 7 bytes are sufficient
        /// but let's add another byte to reduce the probability of future collisions)
        /// </summary>
        public static int SignificantIdBytes = 8;

        public static void GenerateRedirectFile(
            string solutionDestinationFolder,
            string projectDestinationFolder,
            Dictionary<string, IEnumerable<string>> symbolIDToListOfLocationsMap,
            string prefix = "")
        {
            var fileName = Path.Combine(projectDestinationFolder, Constants.IDResolvingFileName + prefix + ".html");

            using (var writer = new StreamWriter(fileName, append: true, encoding: Encoding.UTF8))
            {
                Markup.WriteMetadataToSourceRedirectPrefix(writer);

                if (prefix == "")
                {
                    writer.WriteLine("redirectToNextLevelRedirectFile();");

                    var maps = SplitByFirstLetter(symbolIDToListOfLocationsMap);
                    foreach (var map in maps)
                    {
                        GenerateRedirectFile(
                            solutionDestinationFolder,
                            projectDestinationFolder,
                            map.Value,
                            map.Key.ToString());
                    }
                }
                else
                {
                    WriteMapping(
                        writer,
                        solutionDestinationFolder,
                        projectDestinationFolder,
                        symbolIDToListOfLocationsMap);
                }

                Markup.WriteMetadataToSourceRedirectSuffix(writer);
            }
        }

        public static Dictionary<char, Dictionary<string, IEnumerable<string>>> SplitByFirstLetter(
            Dictionary<string, IEnumerable<string>> symbolIDToListOfLocationsMap)
        {
            var result = new Dictionary<char, Dictionary<string, IEnumerable<string>>>();

            foreach (var kvp in symbolIDToListOfLocationsMap)
            {
                var key = kvp.Key;
                var values = kvp.Value;

                Dictionary<string, IEnumerable<string>> bucket;
                if (!result.TryGetValue(key[0], out bucket))
                {
                    bucket = new Dictionary<string, IEnumerable<string>>();
                    result.Add(key[0], bucket);
                }

                bucket.Add(key, values);
            }

            return result;
        }

        private static void WriteMapping(
            StreamWriter writer,
            string solutionDestinationFolder,
            string projectDestinationFolder,
            Dictionary<string, IEnumerable<string>> symbolIDToListOfLocationsMap)
        {
            var files = ExtractFilePaths(symbolIDToListOfLocationsMap);
            var fileIndexLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            writer.WriteLine("var f = [");
            for (int i = 0; i < files.Length; i++)
            {
                fileIndexLookup.Add(files[i], i);
                writer.WriteLine("\"" + files[i] + "\",");
            }

            writer.WriteLine("];");

            writer.WriteLine("var m = new Object();");

            foreach (var kvp in symbolIDToListOfLocationsMap.OrderBy(kvp => kvp.Key))
            {
                string shortenedKey = GetShortenedKey(kvp.Key);
                var filePaths = kvp.Value;

                if (filePaths.Count() == 1)
                {
                    var value = filePaths.First();
                    writer.WriteLine("m[\"" + shortenedKey + "\"]=f[" + fileIndexLookup[value].ToString() + "];");
                }
                else
                {
                    writer.WriteLine("m[\"" + shortenedKey + "\"]=\"" + Constants.PartialResolvingFileName + "/" + kvp.Key + "\";");
                    Markup.GeneratePartialTypeDisambiguationFile(
                        solutionDestinationFolder,
                        projectDestinationFolder,
                        kvp.Key,
                        filePaths);
                }
            }

            writer.WriteLine("redirect(m, {0});", SignificantIdBytes);
        }

        private static string GetShortenedKey(string key)
        {
            var shortenedKey = key;
            if (shortenedKey.Length > SignificantIdBytes)
            {
                shortenedKey = shortenedKey.Substring(0, SignificantIdBytes);
            }

            // all the keys in this file start with the same prefix, no need to include it
            shortenedKey = shortenedKey.Substring(1);
            return shortenedKey;
        }

        private static string[] ExtractFilePaths(Dictionary<string, IEnumerable<string>> symbolIDToListOfLocationsMap)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in symbolIDToListOfLocationsMap)
            {
                files.UnionWith(kvp.Value);
            }

            var array = files.ToArray();
            Array.Sort(array);

            return array;
        }
    }
}
