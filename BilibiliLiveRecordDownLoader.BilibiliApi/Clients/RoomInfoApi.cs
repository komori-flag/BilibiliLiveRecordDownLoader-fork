using BilibiliApi.Enums;
using BilibiliApi.Model.PlayUrl;
using BilibiliApi.Model.RoomInfo;
using DynamicData;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace BilibiliApi.Clients;

public partial class BilibiliApiClient
{
	#region 获取直播间播放地址

	/// <summary>
	/// 获取直播间播放地址
	/// </summary>
	/// <param name="roomId">房间号（允许短号）</param>
	/// <param name="qn"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	public async Task<RoomPlayInfo?> GetRoomPlayInfoAsync(long roomId, long qn = 10000, CancellationToken token = default)
	{
		string url = $@"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo?room_id={roomId}&no_playurl=0&qn={qn}&platform=web&protocol=0,1&format=0,1,2&codec=0,1";
		return await GetJsonAsync<RoomPlayInfo>(url, token);
	}

	public const string DefaultCodecOrder = @"avc;hevc";
	public const string DefaultFormatOrder = @"fmp4;ts;flv";

	private record StreamUriInfo(string Protocol, string Format, RoomPlayInfoStreamCodec Codec);

	public async Task<(Uri[], string)> GetRoomStreamUriAsync(long roomId, long qn = 10000,
		string? codecOrder = default, string? formatOrder = default,
		CancellationToken cancellationToken = default)
	{
		RoomPlayInfo? message = await GetRoomPlayInfoAsync(roomId, qn, cancellationToken);

		if (message?.Code is not 0)
		{
			if (message?.Message is not null)
			{
				throw new HttpRequestException($@"获取直播地址失败: {message.Message}");
			}
			throw new HttpRequestException(@"获取直播地址失败");
		}

		if (message.Data?.LiveStatus is not LiveStatus.直播)
		{
			throw new HttpRequestException(@"直播间未在直播");
		}

		RoomPlayInfoStream[] playInfo = message.Data.PlayUrlInfo?.PlayUrl?.StreamInfo ?? throw new HttpRequestException(@"获取直播地址失败: 无法找到直播流");

		List<StreamUriInfo> list = new();

		foreach (RoomPlayInfoStream streamInfo in playInfo)
		{
			if (streamInfo.Format is null || streamInfo.ProtocolName is null)
			{
				continue;
			}

			foreach (RoomPlayInfoStreamFormat format in streamInfo.Format)
			{
				if (format.Codec is null || format.FormatName is null)
				{
					continue;
				}

				foreach (RoomPlayInfoStreamCodec codec in format.Codec)
				{
					if (codec.BaseUrl is null || codec.UrlInfo?.FirstOrDefault() is null || codec.CodecName is null)
					{
						continue;
					}

					if (codec.CodecName.Equals(@"hevc", StringComparison.OrdinalIgnoreCase) && format.FormatName.Equals(@"flv", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					list.Add(new StreamUriInfo(streamInfo.ProtocolName, format.FormatName, codec));
				}
			}
		}

		if (!list.Any())
		{
			throw new HttpRequestException(@"获取直播地址失败: 无法找到直播流");
		}

		string[] codecOrderByDescending = GetOrderByDescending(codecOrder, DefaultCodecOrder);
		string[] formatOrderByDescending = GetOrderByDescending(formatOrder, DefaultFormatOrder);

		StreamUriInfo info = list.OrderByDescending(x => codecOrderByDescending.IndexOf(x.Codec.CodecName, StringComparer.OrdinalIgnoreCase))
			.ThenByDescending(x => formatOrderByDescending.IndexOf(x.Format, StringComparer.OrdinalIgnoreCase))
			.First();

		RoomPlayInfoStreamUrlInfo[] uriInfo = info.Codec.UrlInfo!.Where(GetValidUrlInfo).ToArray();

		Uri[] result = new Uri[uriInfo.LongLength];

		string baseUrl = info.Codec.BaseUrl!;

		if (info.Protocol is @"http_hls")
		{
			if (qn is 10000)
			{
				baseUrl = baseUrl.Replace(@"_1500", string.Empty);

				if (info.Format == @"fmp4")
				{
					baseUrl = Regex.Replace(baseUrl, @"(/live-bvc/\d+/live_\d+_\d+)\/index.m3u8\?", @"$1_bluray/index.m3u8?");
				}

				if (info.Format == @"ts")
				{
					baseUrl = Regex.Replace(baseUrl, @"(/live-bvc/\d+/live_\d+_\d+)\.m3u8\?", @"$1_bluray.m3u8?");
				}
			}
		}

		string[] selfBuilt_biliLiveStreamUrl = [
			@"https://c0--cn-gotcha01.bilivideo.com",
			@"https://d0--cn-gotcha01.bilivideo.com",
			@"https://c1--cn-gotcha01.bilivideo.com"
		];

		for (long i = 0; i < result.LongLength; ++i)
		{
			int randomIndex = new Random().Next(0, selfBuilt_biliLiveStreamUrl.Length);
			if (info.Protocol != @"http_stream" && info.Format == @"fmp4")
			{
				result[i] = new Uri(selfBuilt_biliLiveStreamUrl[randomIndex] + baseUrl);
			}
			else if (info.Protocol != @"http_stream" && info.Format == @"ts")
			{
				result[i] = new Uri(uriInfo[i].Host + baseUrl + uriInfo[i].Extra);
			}
			else
			{
				result[i] = new Uri(selfBuilt_biliLiveStreamUrl[randomIndex] + baseUrl + uriInfo[i].Extra);
			}
		}

		return (result, info.Format);

		static string[] GetOrderByDescending(string? order, string defaultValue)
		{
			while (true)
			{
				if (string.IsNullOrWhiteSpace(order))
				{
					order = defaultValue;
					continue;
				}

				string[] r = order.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

				if (r.Length is not 0)
				{
					return r.Reverse().ToArray();
				}

				order = defaultValue;
			}
		}

		static bool GetValidUrlInfo(RoomPlayInfoStreamUrlInfo x)
		{
			return !string.IsNullOrEmpty(x.Host) &&
				x.Host.StartsWith(@"https://") &&
				(
					Regex.IsMatch(x.Host, @"^https?\:\/\/cn-[^/]*.bilivideo.com") ||
					Regex.IsMatch(x.Host, @"^https?\:\/\/[^\/]*cn-gotcha([\d])?01\.bilivideo\.com")
				);
		}
	}

	#endregion

	#region 获取直播间详细信息

	/// <summary>
	/// 获取直播间详细信息
	/// </summary>
	/// <param name="roomId">房间号（允许短号）</param>
	/// <param name="token"></param>
	/// <returns></returns>
	public async Task<RoomInfoMessage?> GetRoomInfoAsync(long roomId, CancellationToken token = default)
	{
		string url = $@"https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom?room_id={roomId}";
		return await GetJsonAsync<RoomInfoMessage>(url, token);
	}

	/// <summary>
	/// 获取直播间详细信息
	/// </summary>
	/// <param name="roomId">房间号（允许短号）</param>
	/// <param name="token"></param>
	/// <returns></returns>
	public async Task<RoomInfoMessage.RoomInfoData> GetRoomInfoDataAsync(long roomId, CancellationToken token = default)
	{
		RoomInfoMessage? roomInfo = await GetRoomInfoAsync(roomId, token);
		if (roomInfo?.data is null || roomInfo.code != 0)
		{
			if (roomInfo?.message is not null)
			{
				throw new HttpRequestException($@"获取房间信息失败: {roomInfo.message}");
			}

			throw new HttpRequestException(@"获取房间信息失败");
		}
		return roomInfo.data;
	}

	#endregion

}
