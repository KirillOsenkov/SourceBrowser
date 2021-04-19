using System.IO;
using Microsoft.SourceBrowser.HtmlGenerator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace HtmlGenerator.Tests
{
    [TestClass]
    public class CommandLineOptionsTests
    {
        [TestMethod]
        public void Server_path_allows_equals_sign_on_each_side_when_quoted()
        {
            var mapping = CommandLineOptions.Parse("/serverPath:\"a=1\"=\"b=2\"").ServerPathMappings.ShouldHaveSingleItem();
            mapping.Key.ShouldBe(Path.GetFullPath("a=1"));
            mapping.Value.ShouldBe("b=2");
        }

        [TestMethod]
        public void Server_path_allows_equals_sign_on_left_side_when_only_left_side_is_quoted()
        {
            var mapping = CommandLineOptions.Parse("/serverPath:\"a=1\"=b").ServerPathMappings.ShouldHaveSingleItem();
            mapping.Key.ShouldBe(Path.GetFullPath("a=1"));
            mapping.Value.ShouldBe("b");
        }

        [TestMethod]
        public void Server_path_allows_equals_sign_on_right_side_when_only_right_side_is_quoted()
        {
            var mapping = CommandLineOptions.Parse("/serverPath:a=\"b=2\"").ServerPathMappings.ShouldHaveSingleItem();
            mapping.Key.ShouldBe(Path.GetFullPath("a"));
            mapping.Value.ShouldBe("b=2");
        }

        [TestMethod]
        public void Server_path_allows_equals_neither_side_to_be_quoted()
        {
            var mapping = CommandLineOptions.Parse("/serverPath:a=b").ServerPathMappings.ShouldHaveSingleItem();
            mapping.Key.ShouldBe(Path.GetFullPath("a"));
            mapping.Value.ShouldBe("b");
        }

        [TestMethod]
        public void Server_path_allows_legacy_quoting()
        {
            var mapping = CommandLineOptions.Parse("/serverPath:\"a 1=b 2\"").ServerPathMappings.ShouldHaveSingleItem();
            mapping.Key.ShouldBe(Path.GetFullPath("a 1"));
            mapping.Value.ShouldBe("b 2");
        }

        [TestMethod]
        public void Server_path_requires_equals_sign()
        {
            CommandLineOptions.Parse("/serverPath:a").ServerPathMappings.ShouldBeEmpty();
        }

        [TestMethod]
        public void Server_path_disallows_further_unquoted_equals_signs_after_the_equals_sign()
        {
            CommandLineOptions.Parse("/serverPath:a=b=2").ServerPathMappings.ShouldBeEmpty();
        }

        [TestMethod]
        public void Server_path_disallows_characters_outside_quotes()
        {
            CommandLineOptions.Parse("/serverPath:c\"a\"=\"b\"").ServerPathMappings.ShouldBeEmpty();
            CommandLineOptions.Parse("/serverPath:\"a\"c=\"b\"").ServerPathMappings.ShouldBeEmpty();
            CommandLineOptions.Parse("/serverPath:\"a\"=c\"b\"").ServerPathMappings.ShouldBeEmpty();
            CommandLineOptions.Parse("/serverPath:\"a\"=\"b\"c").ServerPathMappings.ShouldBeEmpty();
        }
    }
}
