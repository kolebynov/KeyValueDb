using System.Globalization;
using System.Text;
using KeyValueDb;

var smallString = GetString(10);
var mediumString = GetString(100);
var largeString = GetString(1000);

using var db = new Database("test.db");
db.Set("key1", smallString);
db.Set("key2", mediumString);
db.Set("key3", largeString);
db.Set("key4", largeString);
db.Set("key5", largeString);
db.Set("key6", largeString);
db.Set("key7", largeString);
db.Set("key8", largeString);
db.Set("key9", largeString);
db.Set("key10", largeString);
db.Set("key11", largeString);
db.Set("key12", largeString);
db.Set("key13", largeString[..710]);

#pragma warning disable CS8321
static byte[] GetString(int length) =>
#pragma warning restore CS8321
	Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));