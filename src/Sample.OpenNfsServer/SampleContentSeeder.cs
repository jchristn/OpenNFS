namespace Sample.OpenNfsServer
{
    using System.IO;
    using System.Text;

    internal static class SampleContentSeeder
    {
        internal static void EnsureSeeded(string sourcePath)
        {
            Directory.CreateDirectory(sourcePath);
            Directory.CreateDirectory(Path.Combine(sourcePath, "docs"));

            EnsureFile(
                Path.Combine(sourcePath, "hello.txt"),
                "hello-from-sample-opennfs");
            EnsureFile(
                Path.Combine(sourcePath, "docs", "nested.txt"),
                "nested-from-sample-opennfs");
        }

        private static void EnsureFile(string path, string contents)
        {
            if (File.Exists(path))
            {
                return;
            }

            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(contents));
        }
    }
}
