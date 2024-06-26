namespace DocoptNet.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using Assert = NUnit.Framework.Legacy.ClassicAssert;

    [TestFixture]
    public class UsageTests
    {
        [Test]
        public void Test_formal_usage()
        {
            const string doc =
"Usage: prog [-hv] ARG\r\n" +
"       prog N M\r\n" +
"\r\n" +
"       prog is a program.";
            var usage = Docopt.ParseSection("usage:", doc).First();
            Assert.AreEqual("Usage: prog [-hv] ARG\r\n       prog N M", usage);
            Assert.AreEqual("( [-hv] ARG ) | ( N M )", Docopt.FormalUsage(usage));
        }
        [Test]
        public void Should_parse_usage_section_correctly()
        {
            const string usage = @"usage: this

usage:hai
usage: this that

usage: foo
       bar

PROGRAM USAGE:
 foo
 bar
" +
                                 "usage:\r\n\ttoo\r\n" +
                                 "\ttar\r\n" +
                                 @"Usage: eggs spam
BAZZ
usage: pit stop
";

            Assert.IsEmpty(Docopt.ParseSection("usage:", "foo bar fizz buzz"), "No usage");

            Assert.AreEqual(new[] {"usage: prog"}, Docopt.ParseSection("usage:", "usage: prog"), "One line usage");

            Assert.AreEqual(new[] {@"usage: -args\r\n -y"}, Docopt.ParseSection("usage:", @"usage: -args\r\n -y"),
                            "Multi line usage");

            Assert.AreEqual(new[]
                {
                    "usage: this",
                    "usage:hai",
                    "usage: this that",
                    @"usage: foo
       bar",
                    @"PROGRAM USAGE:
 foo
 bar",
                    "usage:\r\n\ttoo\r\n\ttar",
                    "Usage: eggs spam",
                    "usage: pit stop"
                }, Docopt.ParseSection("usage:", usage), "Variations on casing, spaces and tabs");
        }
    }
}
