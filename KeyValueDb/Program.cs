using System.Globalization;
using System.Text;
using KeyValueDb;
using KeyValueDb.Common.Extensions;
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

var smallString = GetString(10);
var mediumString = GetString(100);
var largeString = GetString(1000);

uint pageIndex;
using (var page = pageManager.AllocatePage())
{
	pageIndex = page.PageIndex;
	var recordsPageInit = new RecordsPage();
	page.Write(recordsPageInit.AsReadOnlyBytes());

	using var pageDataAccessor = page.GetRawPageData();
	ref var recordsPage = ref pageDataAccessor.PageData.AsRef<RecordsPage>();

	recordsPage.AddRecord(smallString);
	recordsPage.AddRecord(mediumString);
	recordsPage.AddRecord(largeString);
	recordsPage.AddRecord(mediumString);
	recordsPage.AddRecord(smallString);
	recordsPage.RemoveRecord(1);
	recordsPage.RemoveRecord(2);
	recordsPage.AddRecord(largeString);
	recordsPage.AddRecord(mediumString);

	foreach (var recordIndex in recordsPage.RecordIndices)
	{
		Console.WriteLine($"{recordIndex}: {Encoding.UTF8.GetString(recordsPage.GetRecord(recordIndex))}");
	}
}

pageManager.FreePage(pageIndex);

#pragma warning disable CS8321
static byte[] GetString(int length) =>
#pragma warning restore CS8321
	Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));