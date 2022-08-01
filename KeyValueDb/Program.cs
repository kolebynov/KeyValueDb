using System.Globalization;
using System.Text;
using KeyValueDb.Paging;

using var pageManager = new PageManager(
#pragma warning disable CA2000
	File.Open(
		"test.page",
		new FileStreamOptions
		{
			Access = FileAccess.ReadWrite,
			Mode = FileMode.OpenOrCreate,
			Options = FileOptions.RandomAccess,
			BufferSize = 0,
			Share = FileShare.Read,
		}),
#pragma warning restore CA2000
	0);

using (var page = pageManager.AllocatePage())
{
	page.Write(GetString(10));
}

using (var page = pageManager.AllocatePage())
{
	page.Write(GetString(20));
}

pageManager.FreePage(0);
pageManager.GetPage(0);

#pragma warning disable CS8321
static byte[] GetString(int length) =>
#pragma warning restore CS8321
	Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));