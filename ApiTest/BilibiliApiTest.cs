using BilibiliApi.Clients;
using BilibiliApi.Model.DanmuConf;
using BilibiliApi.Model.FansMedal;
using BilibiliApi.Model.Login.QrCode.GetLoginUrl;
using BilibiliApi.Model.RoomInfo;
using BilibiliLiveRecordDownLoader.Shared.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ApiTest.TestConstants;

namespace ApiTest;

[TestClass]
public class BilibiliApiTest
{
	private readonly BilibiliApiClient _apiClient = new(HttpClientUtils.BuildClientForBilibili(string.Empty, Cookie, new SocketsHttpHandler()));

	[TestMethod]
	public async Task GetDanmuConfTestAsync()
	{
		DanmuConfMessage? json = await _apiClient.GetDanmuConfAsync(866466);
		Assert.IsNotNull(json);
		Assert.AreEqual(0, json.code);
		Assert.AreEqual(@"0", json.message);

		Assert.IsNotNull(json.data);
		Assert.IsNotNull(json.data.host_list);
		Assert.IsTrue(json.data.host_list.Length > 0);
		Assert.IsTrue(!string.IsNullOrWhiteSpace(json.data.token));

		Assert.AreEqual(@"broadcastlv.chat.bilibili.com", json.data.host_list.Last().host);
		Assert.AreEqual(2243, json.data.host_list.Last().port);
		Assert.IsTrue(json.data.host_list.Last().wss_port is 2245 or 443);
		Assert.AreEqual(2244, json.data.host_list.Last().ws_port);
	}

	[TestMethod]
	public async Task GetRoomUriTestAsync()
	{
		(Uri[] hlsUris, string format) = await _apiClient.GetRoomStreamUriAsync(6);

		Assert.AreNotEqual(0, hlsUris.Length);
		Assert.AreNotEqual(@"fmp4", format);

		foreach (Uri hlsUri in hlsUris)
		{
			Assert.AreEqual(Uri.UriSchemeHttps, hlsUri.Scheme);
			Assert.AreEqual(@".m3u8", Path.GetExtension(hlsUri.AbsolutePath));
			Console.WriteLine(hlsUri);
		}
	}

	[TestMethod]
	public async Task GetRoomInfoTestAsync()
	{
		RoomInfoMessage? json = await _apiClient.GetRoomInfoAsync(866466);
		Assert.IsNotNull(json);
		Assert.AreEqual(0, json.code);
		Assert.AreEqual(@"0", json.message);
		Assert.IsNotNull(json.data);

		Assert.IsNotNull(json.data.room_info);
		Assert.AreEqual(6154037, json.data.room_info.room_id);
		Assert.AreEqual(866466, json.data.room_info.short_id);
		Assert.IsTrue(json.data.room_info.live_status is 0 or 1 or 2);
		Assert.IsTrue(!string.IsNullOrWhiteSpace(json.data.room_info.title));

		Assert.IsNotNull(json.data.anchor_info);
		Assert.IsNotNull(json.data.anchor_info.base_info);
		Assert.AreEqual(@"萝卜吃米洛", json.data.anchor_info.base_info.uname);
	}

	[TestMethod]
	public async Task GetLoginUrlTestAsync()
	{
		GetLoginUrlMessage? json = await _apiClient.GetLoginUrlAsync();
		Assert.IsNotNull(json);
		Assert.AreEqual(json.code, 0);
		Assert.AreEqual(json.message, @"0");
		Assert.IsNotNull(json.data);

		Assert.IsNotNull(json.data.url);
		Assert.IsTrue(json.data.url.StartsWith(@"https://"));

		Assert.IsNotNull(json.data.qrcode_key);
		Assert.AreEqual(json.data.qrcode_key.Length, 32);
	}

	[TestMethod]
	public async Task GetLoginInfoTestAsync()
	{
		var cookie = await _apiClient.GetLoginInfoAsync(@""); // 设置 Key
		Assert.IsTrue(cookie.Contains(@"sid="));
		Assert.IsTrue(cookie.Contains(@"DedeUserID="));
		Assert.IsTrue(cookie.Contains(@"DedeUserID__ckMd5="));
		Assert.IsTrue(cookie.Contains(@"SESSDATA="));
		Assert.IsTrue(cookie.Contains(@"bili_jct="));
	}

	[TestMethod]
	public async Task GetLoginInfoFailTestAsync()
	{
		var ex = await Assert.ThrowsExceptionAsync<HttpRequestException>(async () => await _apiClient.GetLoginInfoAsync(string.Empty));
		Assert.AreEqual(ex.Message, @"不存在该密钥");
	}

	[TestMethod]
	public async Task CheckLoginStatusTestAsync()
	{
		Assert.AreNotEqual(await _apiClient.CheckLoginStatusAsync(), string.IsNullOrEmpty(Cookie));
	}

	[TestMethod]
	public async Task GetUidTestAsync()
	{
		Assert.IsTrue(await _apiClient.GetUidAsync() > 0);
	}

	[TestMethod]
	public async Task FansMedalTestAsync()
	{
		LiveFansMedalMessage? message0 = await _apiClient.GetLiveFansMedalMessageAsync();
		Assert.IsNotNull(message0);
		Assert.AreEqual(0, message0.code);
		Assert.IsNotNull(message0.data);
		long count = message0.data.count;

		List<FansMedalList> list = await _apiClient.GetLiveFansMedalListAsync();
		Assert.AreEqual(count, list.Count);
	}

	[TestMethod]
	public async Task DanmuSendTestAsync()
	{
		await _apiClient.SendDanmuAsync(40462, Csrf);
	}
}
