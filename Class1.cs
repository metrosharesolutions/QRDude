#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PureQR
{
	// =========================================================================
	// Public API
	// =========================================================================

	/// <summary>
	/// QR error correction level.
	/// </summary>
	public enum QrErrorCorrectionLevel
	{
		Low,
		Medium,
		Quartile,
		High
	}

	/// <summary>
	/// Simple 24-bit RGB color.
	/// </summary>
	public readonly struct Rgb24
	{
		public byte R { get; }
		public byte G { get; }
		public byte B { get; }

		public Rgb24(byte r, byte g, byte b)
		{
			R = r;
			G = g;
			B = b;
		}

		public static Rgb24 Black => new Rgb24(0, 0, 0);
		public static Rgb24 White => new Rgb24(255, 255, 255);

		public override string ToString()
		{
			return $"#{R:X2}{G:X2}{B:X2}";
		}
	}

	/// <summary>
	/// Rendering options for the BMP output.
	/// </summary>
	public sealed class QrRenderOptions
	{
		/// <summary>
		/// Pixels per module.
		/// </summary>
		public int Scale { get; set; } = 10;

		/// <summary>
		/// Quiet zone size in modules.
		/// </summary>
		public int Border { get; set; } = 4;

		/// <summary>
		/// Dark module color.
		/// </summary>
		public Rgb24 DarkColor { get; set; } = Rgb24.Black;

		/// <summary>
		/// Light module color.
		/// </summary>
		public Rgb24 LightColor { get; set; } = Rgb24.White;
	}

	/// <summary>
	/// Full create options.
	/// </summary>
	public sealed class QrCreateOptions
	{
		/// <summary>
		/// Error correction level.
		/// </summary>
		public QrErrorCorrectionLevel ErrorCorrectionLevel { get; set; } = QrErrorCorrectionLevel.Medium;

		/// <summary>
		/// Minimum version to consider. 1-40.
		/// </summary>
		public int MinVersion { get; set; } = 1;

		/// <summary>
		/// Maximum version to consider. 1-40.
		/// </summary>
		public int MaxVersion { get; set; } = 40;

		/// <summary>
		/// Optional fixed mask pattern. 0-7. Null means auto-select best mask.
		/// </summary>
		public int? Mask { get; set; }

		/// <summary>
		/// If true, the encoder may raise ECC level if the data still fits
		/// in the chosen version range.
		/// </summary>
		public bool BoostErrorCorrection { get; set; } = false;

		/// <summary>
		/// If true, generic text input may use numeric/alphanumeric modes when possible.
		/// If false, text is always encoded as UTF-8 bytes.
		/// </summary>
		public bool OptimizeTextMode { get; set; } = true;

		/// <summary>
		/// Output rendering options.
		/// </summary>
		public QrRenderOptions Render { get; set; } = new QrRenderOptions();
	}

	/// <summary>
	/// Full QR creation result.
	/// </summary>
	public sealed class QrBitmapResult
	{
		public string Input { get; init; } = "";
		public int Version { get; init; }
		public int Size { get; init; }
		public int Mask { get; init; }
		public QrErrorCorrectionLevel ErrorCorrectionLevel { get; init; }
		public bool[,] Modules { get; init; } = new bool[0, 0];
		public byte[] BmpBytes { get; init; } = Array.Empty<byte>();
	}

	/// <summary>
	/// Main entry point.
	/// </summary>
	public static class QrBitmapGenerator
	{
		/// <summary>
		/// Create a QR BMP from a URL.
		/// </summary>
		public static byte[] CreateUrlBmp(string url)
		{
			return CreateUrl(url, null).BmpBytes;
		}

		/// <summary>
		/// Create a QR BMP from a URL with options.
		/// </summary>
		public static byte[] CreateUrlBmp(string url, QrCreateOptions? options)
		{
			return CreateUrl(url, options).BmpBytes;
		}

		/// <summary>
		/// Create a QR BMP from generic text.
		/// </summary>
		public static byte[] CreateBmp(string text)
		{
			return Create(text, null).BmpBytes;
		}

		/// <summary>
		/// Create a QR BMP from generic text with options.
		/// </summary>
		public static byte[] CreateBmp(string text, QrCreateOptions? options)
		{
			return Create(text, options).BmpBytes;
		}

		/// <summary>
		/// Try-create a QR BMP from a URL.
		/// </summary>
		public static bool TryCreateUrlBmp(string url, out byte[] bmpBytes)
		{
			try
			{
				bmpBytes = CreateUrlBmp(url);
				return true;
			}
			catch
			{
				bmpBytes = Array.Empty<byte>();
				return false;
			}
		}

		/// <summary>
		/// Try-create a QR BMP from a URL with options.
		/// </summary>
		public static bool TryCreateUrlBmp(string url, QrCreateOptions? options, out byte[] bmpBytes)
		{
			try
			{
				bmpBytes = CreateUrlBmp(url, options);
				return true;
			}
			catch
			{
				bmpBytes = Array.Empty<byte>();
				return false;
			}
		}

		/// <summary>
		/// Try-create a QR BMP from generic text.
		/// </summary>
		public static bool TryCreateBmp(string text, out byte[] bmpBytes)
		{
			try
			{
				bmpBytes = CreateBmp(text);
				return true;
			}
			catch
			{
				bmpBytes = Array.Empty<byte>();
				return false;
			}
		}

		/// <summary>
		/// Try-create a QR BMP from generic text with options.
		/// </summary>
		public static bool TryCreateBmp(string text, QrCreateOptions? options, out byte[] bmpBytes)
		{
			try
			{
				bmpBytes = CreateBmp(text, options);
				return true;
			}
			catch
			{
				bmpBytes = Array.Empty<byte>();
				return false;
			}
		}

		/// <summary>
		/// Create a full result object from a URL.
		/// </summary>
		public static QrBitmapResult CreateUrl(string url, QrCreateOptions? options = null)
		{
			Guard.NotNullOrWhiteSpace(url, nameof(url));
			Guard.HttpOrHttpsUrl(url, nameof(url));

			options ??= new QrCreateOptions();

			return CreateInternal(url, options, isUrl: true);
		}

		/// <summary>
		/// Create a full result object from text.
		/// </summary>
		public static QrBitmapResult Create(string text, QrCreateOptions? options = null)
		{
			Guard.NotNullOrWhiteSpace(text, nameof(text));

			options ??= new QrCreateOptions();

			return CreateInternal(text, options, isUrl: false);
		}

		/// <summary>
		/// Save a URL QR directly to a BMP file.
		/// </summary>
		public static void SaveUrlBmp(string url, string filePath, QrCreateOptions? options = null)
		{
			Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));
			File.WriteAllBytes(filePath, CreateUrlBmp(url, options));
		}

		/// <summary>
		/// Save a text QR directly to a BMP file.
		/// </summary>
		public static void SaveBmp(string text, string filePath, QrCreateOptions? options = null)
		{
			Guard.NotNullOrWhiteSpace(filePath, nameof(filePath));
			File.WriteAllBytes(filePath, CreateBmp(text, options));
		}

		private static QrBitmapResult CreateInternal(string input, QrCreateOptions options, bool isUrl)
		{
			Guard.VersionRange(options.MinVersion, nameof(options.MinVersion));
			Guard.VersionRange(options.MaxVersion, nameof(options.MaxVersion));

			if (options.MinVersion > options.MaxVersion)
				throw new ArgumentException("MinVersion must be <= MaxVersion.");

			if (options.Mask is not null && (options.Mask < 0 || options.Mask > 7))
				throw new ArgumentOutOfRangeException(nameof(options.Mask), "Mask must be 0 through 7.");

			Guard.Positive(options.Render.Scale, nameof(options.Render.Scale));
			if (options.Render.Border < 0)
				throw new ArgumentOutOfRangeException(nameof(options.Render.Border), "Border must be >= 0.");

			QrSegment segment;

			if (options.OptimizeTextMode)
			{
				segment = QrSegment.MakeBest(input);
			}
			else
			{
				segment = QrSegment.MakeBytes(Encoding.UTF8.GetBytes(input));
			}

			var segments = new List<QrSegment>(1) { segment };

			QrCode qr = QrCode.EncodeSegments(
				segments,
				options.ErrorCorrectionLevel,
				options.MinVersion,
				options.MaxVersion,
				options.Mask,
				options.BoostErrorCorrection
			);

			bool[,] modules = qr.GetModulesCopy();

			byte[] bmp = BmpWriter.Write24BitBmp(
				modules,
				qr.Size,
				options.Render.Scale,
				options.Render.Border,
				options.Render.DarkColor,
				options.Render.LightColor
			);

			return new QrBitmapResult
			{
				Input = input,
				Version = qr.Version,
				Size = qr.Size,
				Mask = qr.Mask,
				ErrorCorrectionLevel = qr.ErrorCorrectionLevel,
				Modules = modules,
				BmpBytes = bmp
			};
		}
	}

	// =========================================================================
	// Internal mode model
	// =========================================================================

	internal enum QrMode
	{
		Numeric = 0x1,
		Alphanumeric = 0x2,
		Byte = 0x4
	}

	internal static class QrModeExtensions
	{
		public static int ModeBits(this QrMode mode)
		{
			return (int)mode;
		}

		public static int CharCountBits(this QrMode mode, int version)
		{
			Guard.VersionRange(version, nameof(version));

			if (version <= 9)
			{
				return mode switch
				{
					QrMode.Numeric => 10,
					QrMode.Alphanumeric => 9,
					QrMode.Byte => 8,
					_ => throw new InvalidOperationException("Unsupported mode.")
				};
			}

			if (version <= 26)
			{
				return mode switch
				{
					QrMode.Numeric => 12,
					QrMode.Alphanumeric => 11,
					QrMode.Byte => 16,
					_ => throw new InvalidOperationException("Unsupported mode.")
				};
			}

			return mode switch
			{
				QrMode.Numeric => 14,
				QrMode.Alphanumeric => 13,
				QrMode.Byte => 16,
				_ => throw new InvalidOperationException("Unsupported mode.")
			};
		}
	}

	// =========================================================================
	// Segment
	// =========================================================================

	internal sealed class QrSegment
	{
		private const string AlphanumericCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

		private static readonly int[] AlphanumericMap = BuildAlphanumericMap();

		public QrMode Mode { get; }
		public int NumChars { get; }
		public byte[] Data { get; }
		public int BitLength { get; }

		private QrSegment(QrMode mode, int numChars, byte[] data, int bitLength)
		{
			Mode = mode;
			NumChars = numChars;
			Data = data;
			BitLength = bitLength;
		}

		public static QrSegment MakeBest(string text)
		{
			Guard.NotNull(text, nameof(text));

			if (text.Length == 0)
				return MakeBytes(Array.Empty<byte>());

			if (IsNumeric(text))
				return MakeNumeric(text);

			if (IsAlphanumeric(text))
				return MakeAlphanumeric(text);

			return MakeBytes(Encoding.UTF8.GetBytes(text));
		}

		public static QrSegment MakeBytes(byte[] data)
		{
			Guard.NotNull(data, nameof(data));

			byte[] copy = new byte[data.Length];
			Buffer.BlockCopy(data, 0, copy, 0, data.Length);

			return new QrSegment(QrMode.Byte, copy.Length, copy, copy.Length * 8);
		}

		public static QrSegment MakeNumeric(string digits)
		{
			Guard.NotNull(digits, nameof(digits));

			if (!IsNumeric(digits))
				throw new ArgumentException("Input contains non-numeric characters.", nameof(digits));

			BitBuffer bb = new BitBuffer();

			for (int i = 0; i < digits.Length;)
			{
				int n = Math.Min(3, digits.Length - i);
				int val = int.Parse(digits.AsSpan(i, n));
				if (n == 3)
					bb.AppendBits((uint)val, 10);
				else if (n == 2)
					bb.AppendBits((uint)val, 7);
				else
					bb.AppendBits((uint)val, 4);

				i += n;
			}

			return new QrSegment(QrMode.Numeric, digits.Length, bb.ToByteArray(), bb.BitLength);
		}

		public static QrSegment MakeAlphanumeric(string text)
		{
			Guard.NotNull(text, nameof(text));

			if (!IsAlphanumeric(text))
				throw new ArgumentException("Input contains characters not supported by QR alphanumeric mode.", nameof(text));

			BitBuffer bb = new BitBuffer();

			int i = 0;
			while (i + 1 < text.Length)
			{
				int a = AlphanumericValue(text[i]);
				int b = AlphanumericValue(text[i + 1]);
				int value = a * 45 + b;
				bb.AppendBits((uint)value, 11);
				i += 2;
			}

			if (i < text.Length)
			{
				int value = AlphanumericValue(text[i]);
				bb.AppendBits((uint)value, 6);
			}

			return new QrSegment(QrMode.Alphanumeric, text.Length, bb.ToByteArray(), bb.BitLength);
		}

		public void AppendTo(BitBuffer bb)
		{
			Guard.NotNull(bb, nameof(bb));

			for (int i = 0; i < BitLength; i++)
			{
				bool bit = BitHelper.GetBit(Data, i);
				bb.AppendBit(bit);
			}
		}

		public static long GetTotalBits(IReadOnlyList<QrSegment> segments, int version)
		{
			Guard.NotNull(segments, nameof(segments));
			Guard.VersionRange(version, nameof(version));

			long total = 0;

			for (int i = 0; i < segments.Count; i++)
			{
				QrSegment seg = segments[i];
				int ccbits = seg.Mode.CharCountBits(version);

				if (seg.NumChars >= (1 << ccbits))
					return -1;

				total += 4L + ccbits + seg.BitLength;

				if (total > int.MaxValue)
					return -1;
			}

			return total;
		}

		private static bool IsNumeric(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c < '0' || c > '9')
					return false;
			}

			return true;
		}

		private static bool IsAlphanumeric(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c >= AlphanumericMap.Length || AlphanumericMap[c] < 0)
					return false;
			}

			return true;
		}

		private static int AlphanumericValue(char c)
		{
			if (c >= AlphanumericMap.Length)
				throw new ArgumentOutOfRangeException(nameof(c));

			int value = AlphanumericMap[c];
			if (value < 0)
				throw new ArgumentException($"Character '{c}' is not in the QR alphanumeric set.");

			return value;
		}

		private static int[] BuildAlphanumericMap()
		{
			int[] map = new int[128];
			for (int i = 0; i < map.Length; i++)
				map[i] = -1;

			for (int i = 0; i < AlphanumericCharset.Length; i++)
				map[AlphanumericCharset[i]] = i;

			return map;
		}
	}

	// =========================================================================
	// Bit buffer
	// =========================================================================

	internal sealed class BitBuffer
	{
		private readonly List<byte> _bytes = new List<byte>();
		private int _bitLength;

		public int BitLength => _bitLength;

		public void AppendBit(bool value)
		{
			int byteIndex = _bitLength >> 3;
			int bitInByte = 7 - (_bitLength & 7);

			if (byteIndex == _bytes.Count)
				_bytes.Add(0);

			if (value)
				_bytes[byteIndex] |= (byte)(1 << bitInByte);

			_bitLength++;
		}

		public void AppendBits(uint value, int bitCount)
		{
			if (bitCount < 0 || bitCount > 31)
				throw new ArgumentOutOfRangeException(nameof(bitCount));

			if (bitCount < 31 && (value >> bitCount) != 0)
				throw new ArgumentException("Value contains bits outside the requested width.", nameof(value));

			for (int i = bitCount - 1; i >= 0; i--)
				AppendBit(((value >> i) & 1) != 0);
		}

		public void AppendByte(byte value)
		{
			AppendBits(value, 8);
		}

		public void AppendBytes(byte[] data)
		{
			Guard.NotNull(data, nameof(data));

			for (int i = 0; i < data.Length; i++)
				AppendByte(data[i]);
		}

		public byte[] ToByteArray()
		{
			return _bytes.ToArray();
		}
	}

	// =========================================================================
	// QR core
	// =========================================================================

	internal sealed class QrCode
	{
		private readonly bool[,] _modules;
		private readonly bool[,] _isFunction;
		private readonly int _size;

		public int Version { get; }
		public int Size => _size;
		public int Mask { get; private set; }
		public QrErrorCorrectionLevel ErrorCorrectionLevel { get; }

		private QrCode(int version, QrErrorCorrectionLevel ecl, byte[] dataCodewords, int? requestedMask)
		{
			Version = version;
			ErrorCorrectionLevel = ecl;
			_size = version * 4 + 17;

			_modules = new bool[_size, _size];
			_isFunction = new bool[_size, _size];

			DrawFunctionPatterns();

			byte[] allCodewords = AddErrorCorrectionAndInterleave(dataCodewords);
			DrawCodewords(allCodewords);

			if (requestedMask is int fixedMask)
			{
				ApplyMask(fixedMask);
				DrawFormatBits(fixedMask);
				Mask = fixedMask;
			}
			else
			{
				int bestMask = 0;
				int bestPenalty = int.MaxValue;

				for (int mask = 0; mask < 8; mask++)
				{
					ApplyMask(mask);
					DrawFormatBits(mask);

					int penalty = GetPenaltyScore();

					if (penalty < bestPenalty)
					{
						bestPenalty = penalty;
						bestMask = mask;
					}

					ApplyMask(mask);
				}

				ApplyMask(bestMask);
				DrawFormatBits(bestMask);
				Mask = bestMask;
			}
		}

		public static QrCode EncodeSegments(
			IReadOnlyList<QrSegment> segments,
			QrErrorCorrectionLevel ecl,
			int minVersion,
			int maxVersion,
			int? mask,
			bool boostEcl)
		{
			Guard.NotNull(segments, nameof(segments));
			Guard.VersionRange(minVersion, nameof(minVersion));
			Guard.VersionRange(maxVersion, nameof(maxVersion));

			if (minVersion > maxVersion)
				throw new ArgumentException("minVersion must be <= maxVersion.");

			if (mask is not null && (mask < 0 || mask > 7))
				throw new ArgumentOutOfRangeException(nameof(mask), "Mask must be between 0 and 7.");

			int version = -1;
			long usedBits = -1;

			for (int ver = minVersion; ver <= maxVersion; ver++)
			{
				long bits = QrSegment.GetTotalBits(segments, ver);
				if (bits < 0)
					continue;

				int capacityBits = QrTables.GetNumDataCodewords(ver, ecl) * 8;
				if (bits <= capacityBits)
				{
					version = ver;
					usedBits = bits;
					break;
				}
			}

			if (version == -1)
				throw new InvalidOperationException("Data does not fit in any allowed QR version.");

			if (boostEcl)
			{
				foreach (QrErrorCorrectionLevel candidate in GetBoostCandidates(ecl))
				{
					int capacityBits = QrTables.GetNumDataCodewords(version, candidate) * 8;
					if (usedBits <= capacityBits)
						ecl = candidate;
				}
			}

			int dataCapacityBits = QrTables.GetNumDataCodewords(version, ecl) * 8;

			BitBuffer bb = new BitBuffer();

			for (int i = 0; i < segments.Count; i++)
			{
				QrSegment seg = segments[i];
				bb.AppendBits((uint)seg.Mode.ModeBits(), 4);
				bb.AppendBits((uint)seg.NumChars, seg.Mode.CharCountBits(version));
				seg.AppendTo(bb);
			}

			int terminatorBits = Math.Min(4, dataCapacityBits - bb.BitLength);
			bb.AppendBits(0, terminatorBits);

			int alignBits = (8 - (bb.BitLength & 7)) & 7;
			bb.AppendBits(0, alignBits);

			int padByte = 0xEC;
			while (bb.BitLength < dataCapacityBits)
			{
				bb.AppendBits((uint)padByte, 8);
				padByte = padByte == 0xEC ? 0x11 : 0xEC;
			}

			byte[] dataCodewords = bb.ToByteArray();

			if (dataCodewords.Length != QrTables.GetNumDataCodewords(version, ecl))
				throw new InvalidOperationException("Internal error: data codeword count mismatch.");

			return new QrCode(version, ecl, dataCodewords, mask);
		}

		public bool[,] GetModulesCopy()
		{
			bool[,] copy = new bool[_size, _size];

			for (int y = 0; y < _size; y++)
			{
				for (int x = 0; x < _size; x++)
				{
					copy[x, y] = _modules[x, y];
				}
			}

			return copy;
		}

		private static IEnumerable<QrErrorCorrectionLevel> GetBoostCandidates(QrErrorCorrectionLevel current)
		{
			switch (current)
			{
				case QrErrorCorrectionLevel.Low:
					yield return QrErrorCorrectionLevel.Medium;
					yield return QrErrorCorrectionLevel.Quartile;
					yield return QrErrorCorrectionLevel.High;
					yield break;

				case QrErrorCorrectionLevel.Medium:
					yield return QrErrorCorrectionLevel.Quartile;
					yield return QrErrorCorrectionLevel.High;
					yield break;

				case QrErrorCorrectionLevel.Quartile:
					yield return QrErrorCorrectionLevel.High;
					yield break;

				case QrErrorCorrectionLevel.High:
				default:
					yield break;
			}
		}

		private void DrawFunctionPatterns()
		{
			// Finder patterns + separators
			DrawFinderPattern(3, 3);
			DrawFinderPattern(_size - 4, 3);
			DrawFinderPattern(3, _size - 4);

			// Timing patterns
			for (int i = 0; i < _size; i++)
			{
				SetFunctionModule(6, i, i % 2 == 0);
				SetFunctionModule(i, 6, i % 2 == 0);
			}

			// Alignment patterns
			int[] align = QrTables.GetAlignmentPatternPositions(Version);
			int numAlign = align.Length;

			for (int i = 0; i < numAlign; i++)
			{
				for (int j = 0; j < numAlign; j++)
				{
					bool isCorner =
						(i == 0 && j == 0) ||
						(i == 0 && j == numAlign - 1) ||
						(i == numAlign - 1 && j == 0);

					if (!isCorner)
						DrawAlignmentPattern(align[i], align[j]);
				}
			}

			// Dark module
			SetFunctionModule(8, _size - 8, true);

			// Reserve / write version info
			if (Version >= 7)
				DrawVersion();

			// Reserve / dummy format bits; overwritten after mask selection
			DrawFormatBits(0);
		}

		private void DrawFinderPattern(int centerX, int centerY)
		{
			for (int dy = -4; dy <= 4; dy++)
			{
				for (int dx = -4; dx <= 4; dx++)
				{
					int x = centerX + dx;
					int y = centerY + dy;

					if (x < 0 || x >= _size || y < 0 || y >= _size)
						continue;

					int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));

					bool dark = dist != 2 && dist != 4;
					SetFunctionModule(x, y, dark);
				}
			}
		}

		private void DrawAlignmentPattern(int centerX, int centerY)
		{
			for (int dy = -2; dy <= 2; dy++)
			{
				for (int dx = -2; dx <= 2; dx++)
				{
					int x = centerX + dx;
					int y = centerY + dy;

					int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
					bool dark = dist == 0 || dist == 2;

					SetFunctionModule(x, y, dark);
				}
			}
		}

		private void DrawFormatBits(int mask)
		{
			int data = (QrTables.GetFormatBitsForEcl(ErrorCorrectionLevel) << 3) | mask;
			int rem = data << 10;

			for (int i = 14; i >= 10; i--)
			{
				if (((rem >> i) & 1) != 0)
					rem ^= 0x537 << (i - 10);
			}

			int bits = ((data << 10) | rem) ^ 0x5412;

			for (int i = 0; i <= 5; i++)
				SetFunctionModule(8, i, BitHelper.GetBit(bits, i));

			SetFunctionModule(8, 7, BitHelper.GetBit(bits, 6));
			SetFunctionModule(8, 8, BitHelper.GetBit(bits, 7));
			SetFunctionModule(7, 8, BitHelper.GetBit(bits, 8));

			for (int i = 9; i < 15; i++)
				SetFunctionModule(14 - i, 8, BitHelper.GetBit(bits, i));

			for (int i = 0; i < 8; i++)
				SetFunctionModule(_size - 1 - i, 8, BitHelper.GetBit(bits, i));

			for (int i = 8; i < 15; i++)
				SetFunctionModule(8, _size - 15 + i, BitHelper.GetBit(bits, i));

			SetFunctionModule(8, _size - 8, true);
		}

		private void DrawVersion()
		{
			int rem = Version << 12;

			for (int i = 17; i >= 12; i--)
			{
				if (((rem >> i) & 1) != 0)
					rem ^= 0x1F25 << (i - 12);
			}

			int bits = (Version << 12) | rem;

			for (int i = 0; i < 18; i++)
			{
				bool bit = BitHelper.GetBit(bits, i);
				int a = _size - 11 + (i % 3);
				int b = i / 3;

				SetFunctionModule(a, b, bit);
				SetFunctionModule(b, a, bit);
			}
		}

		private byte[] AddErrorCorrectionAndInterleave(byte[] data)
		{
			int numBlocks = QrTables.GetNumErrorCorrectionBlocks(Version, ErrorCorrectionLevel);
			int eccPerBlock = QrTables.GetEccCodewordsPerBlock(Version, ErrorCorrectionLevel);
			int rawCodewords = QrTables.GetNumRawDataModules(Version) / 8;
			int shortBlockLen = rawCodewords / numBlocks;
			int numShortBlocks = numBlocks - (rawCodewords % numBlocks);
			int shortDataLen = shortBlockLen - eccPerBlock;

			byte[] divisor = ReedSolomon.GetDivisor(eccPerBlock);

			var blocks = new List<DataEccBlock>(numBlocks);

			int dataOffset = 0;
			for (int i = 0; i < numBlocks; i++)
			{
				int dataLen = shortDataLen + (i < numShortBlocks ? 0 : 1);

				byte[] blockData = new byte[dataLen];
				Buffer.BlockCopy(data, dataOffset, blockData, 0, dataLen);
				dataOffset += dataLen;

				byte[] ecc = ReedSolomon.ComputeRemainder(blockData, divisor);

				blocks.Add(new DataEccBlock(blockData, ecc));
			}

			if (dataOffset != data.Length)
				throw new InvalidOperationException("Internal error: data block split mismatch.");

			var result = new List<byte>(rawCodewords);

			int maxDataLen = shortDataLen + 1;
			for (int i = 0; i < maxDataLen; i++)
			{
				for (int b = 0; b < blocks.Count; b++)
				{
					if (i < blocks[b].Data.Length)
						result.Add(blocks[b].Data[i]);
				}
			}

			for (int i = 0; i < eccPerBlock; i++)
			{
				for (int b = 0; b < blocks.Count; b++)
					result.Add(blocks[b].Ecc[i]);
			}

			if (result.Count != rawCodewords)
				throw new InvalidOperationException("Internal error: codeword interleave mismatch.");

			return result.ToArray();
		}

		private void DrawCodewords(byte[] data)
		{
			int bitIndex = 0;
			bool upward = true;

			for (int right = _size - 1; right >= 1; right -= 2)
			{
				if (right == 6)
					right--;

				for (int row = 0; row < _size; row++)
				{
					int y = upward ? _size - 1 - row : row;

					for (int col = 0; col < 2; col++)
					{
						int x = right - col;
						if (_isFunction[x, y])
							continue;

						bool dark = false;
						if (bitIndex < data.Length * 8)
							dark = BitHelper.GetBit(data, bitIndex);

						_modules[x, y] = dark;
						bitIndex++;
					}
				}

				upward = !upward;
			}
		}

		private void ApplyMask(int mask)
		{
			for (int y = 0; y < _size; y++)
			{
				for (int x = 0; x < _size; x++)
				{
					if (_isFunction[x, y])
						continue;

					if (MaskBit(mask, x, y))
						_modules[x, y] = !_modules[x, y];
				}
			}
		}

		private static bool MaskBit(int mask, int x, int y)
		{
			return mask switch
			{
				0 => ((x + y) & 1) == 0,
				1 => (y & 1) == 0,
				2 => x % 3 == 0,
				3 => (x + y) % 3 == 0,
				4 => (((y / 2) + (x / 3)) & 1) == 0,
				5 => ((x * y) % 2 + (x * y) % 3) == 0,
				6 => (((x * y) % 2 + (x * y) % 3) & 1) == 0,
				7 => ((((x + y) % 2) + ((x * y) % 3)) & 1) == 0,
				_ => throw new ArgumentOutOfRangeException(nameof(mask))
			};
		}

		private int GetPenaltyScore()
		{
			int score = 0;

			// Rule 1: adjacent modules in row having same color
			for (int y = 0; y < _size; y++)
			{
				bool color = _modules[0, y];
				int runLength = 1;

				for (int x = 1; x < _size; x++)
				{
					if (_modules[x, y] == color)
					{
						runLength++;
					}
					else
					{
						if (runLength >= 5)
							score += 3 + (runLength - 5);

						color = _modules[x, y];
						runLength = 1;
					}
				}

				if (runLength >= 5)
					score += 3 + (runLength - 5);
			}

			// Rule 1: adjacent modules in column having same color
			for (int x = 0; x < _size; x++)
			{
				bool color = _modules[x, 0];
				int runLength = 1;

				for (int y = 1; y < _size; y++)
				{
					if (_modules[x, y] == color)
					{
						runLength++;
					}
					else
					{
						if (runLength >= 5)
							score += 3 + (runLength - 5);

						color = _modules[x, y];
						runLength = 1;
					}
				}

				if (runLength >= 5)
					score += 3 + (runLength - 5);
			}

			// Rule 2: 2x2 blocks of same color
			for (int y = 0; y < _size - 1; y++)
			{
				for (int x = 0; x < _size - 1; x++)
				{
					bool c = _modules[x, y];
					if (c == _modules[x + 1, y] &&
						c == _modules[x, y + 1] &&
						c == _modules[x + 1, y + 1])
					{
						score += 3;
					}
				}
			}

			// Rule 3: finder-like pattern in rows
			for (int y = 0; y < _size; y++)
			{
				for (int x = 0; x <= _size - 11; x++)
				{
					if (MatchesFinderLikePatternRow(x, y))
						score += 40;
				}
			}

			// Rule 3: finder-like pattern in columns
			for (int x = 0; x < _size; x++)
			{
				for (int y = 0; y <= _size - 11; y++)
				{
					if (MatchesFinderLikePatternColumn(x, y))
						score += 40;
				}
			}

			// Rule 4: dark/light balance
			int darkCount = 0;
			for (int y = 0; y < _size; y++)
			{
				for (int x = 0; x < _size; x++)
				{
					if (_modules[x, y])
						darkCount++;
				}
			}

			int total = _size * _size;
			int k = Math.Abs(darkCount * 20 - total * 10) / total;
			score += k * 10;

			return score;
		}

		private bool MatchesFinderLikePatternRow(int x, int y)
		{
			bool[] p1 = new[]
			{
				false, false, false, false, true, false, true, true, true, false, true
			};

			bool[] p2 = new[]
			{
				true, false, true, true, true, false, true, false, false, false, false
			};

			bool match1 = true;
			bool match2 = true;

			for (int i = 0; i < 11; i++)
			{
				bool val = _modules[x + i, y];
				if (val != p1[i]) match1 = false;
				if (val != p2[i]) match2 = false;
				if (!match1 && !match2) return false;
			}

			return match1 || match2;
		}

		private bool MatchesFinderLikePatternColumn(int x, int y)
		{
			bool[] p1 = new[]
			{
				false, false, false, false, true, false, true, true, true, false, true
			};

			bool[] p2 = new[]
			{
				true, false, true, true, true, false, true, false, false, false, false
			};

			bool match1 = true;
			bool match2 = true;

			for (int i = 0; i < 11; i++)
			{
				bool val = _modules[x, y + i];
				if (val != p1[i]) match1 = false;
				if (val != p2[i]) match2 = false;
				if (!match1 && !match2) return false;
			}

			return match1 || match2;
		}

		private void SetFunctionModule(int x, int y, bool isDark)
		{
			_modules[x, y] = isDark;
			_isFunction[x, y] = true;
		}

		private sealed class DataEccBlock
		{
			public byte[] Data { get; }
			public byte[] Ecc { get; }

			public DataEccBlock(byte[] data, byte[] ecc)
			{
				Data = data;
				Ecc = ecc;
			}
		}
	}

	// =========================================================================
	// Reed-Solomon
	// =========================================================================

	internal static class ReedSolomon
	{
		private static readonly byte[] ExpTable = BuildExpTable();
		private static readonly byte[] LogTable = BuildLogTable(ExpTable);

		private static readonly Dictionary<int, byte[]> DivisorCache = new Dictionary<int, byte[]>();
		private static readonly object CacheLock = new object();

		public static byte[] GetDivisor(int degree)
		{
			if (degree <= 0)
				throw new ArgumentOutOfRangeException(nameof(degree));

			lock (CacheLock)
			{
				if (DivisorCache.TryGetValue(degree, out byte[]? cached))
					return cached;

				byte[] divisor = BuildDivisor(degree);
				DivisorCache[degree] = divisor;
				return divisor;
			}
		}

		public static byte[] ComputeRemainder(byte[] data, byte[] divisor)
		{
			Guard.NotNull(data, nameof(data));
			Guard.NotNull(divisor, nameof(divisor));

			byte[] result = new byte[divisor.Length];

			for (int i = 0; i < data.Length; i++)
			{
				byte factor = (byte)(data[i] ^ result[0]);

				for (int j = 0; j < result.Length - 1; j++)
				{
					result[j] = (byte)(result[j + 1] ^ Multiply(divisor[j], factor));
				}

				result[result.Length - 1] = Multiply(divisor[result.Length - 1], factor);
			}

			return result;
		}

		private static byte[] BuildDivisor(int degree)
		{
			// Full generator polynomial coefficients, highest power first.
			byte[] poly = new byte[] { 1 };

			for (int i = 0; i < degree; i++)
			{
				byte root = ExpTable[i]; // alpha^i

				byte[] next = new byte[poly.Length + 1];

				for (int j = 0; j < poly.Length; j++)
				{
					next[j] ^= poly[j];
					next[j + 1] ^= Multiply(poly[j], root);
				}

				poly = next;
			}

			// Omit leading coefficient (which is always 1)
			byte[] result = new byte[degree];
			Buffer.BlockCopy(poly, 1, result, 0, degree);
			return result;
		}

		private static byte Multiply(byte x, byte y)
		{
			if (x == 0 || y == 0)
				return 0;

			int log = LogTable[x] + LogTable[y];
			return ExpTable[log];
		}

		private static byte[] BuildExpTable()
		{
			byte[] exp = new byte[512];
			int x = 1;

			for (int i = 0; i < 255; i++)
			{
				exp[i] = (byte)x;
				x <<= 1;
				if ((x & 0x100) != 0)
					x ^= 0x11D;
			}

			for (int i = 255; i < exp.Length; i++)
				exp[i] = exp[i - 255];

			return exp;
		}

		private static byte[] BuildLogTable(byte[] expTable)
		{
			byte[] log = new byte[256];

			for (int i = 0; i < 255; i++)
				log[expTable[i]] = (byte)i;

			return log;
		}
	}

	// =========================================================================
	// BMP writer
	// =========================================================================

	internal static class BmpWriter
	{
		public static byte[] Write24BitBmp(
			bool[,] modules,
			int moduleSize,
			int scale,
			int border,
			Rgb24 darkColor,
			Rgb24 lightColor)
		{
			Guard.NotNull(modules, nameof(modules));
			Guard.Positive(scale, nameof(scale));

			if (border < 0)
				throw new ArgumentOutOfRangeException(nameof(border));

			int widthModules = moduleSize + border * 2;
			int heightModules = moduleSize + border * 2;

			int widthPixels = widthModules * scale;
			int heightPixels = heightModules * scale;

			int rowStride = widthPixels * 3;
			int paddedRowStride = (rowStride + 3) & ~3;

			int pixelDataSize = paddedRowStride * heightPixels;
			int fileSize = 14 + 40 + pixelDataSize;

			byte[] bmp = new byte[fileSize];

			// BITMAPFILEHEADER
			bmp[0] = (byte)'B';
			bmp[1] = (byte)'M';
			WriteInt32LE(bmp, 2, fileSize);
			WriteInt32LE(bmp, 10, 14 + 40);

			// BITMAPINFOHEADER
			WriteInt32LE(bmp, 14, 40);
			WriteInt32LE(bmp, 18, widthPixels);
			WriteInt32LE(bmp, 22, heightPixels);
			WriteInt16LE(bmp, 26, 1);
			WriteInt16LE(bmp, 28, 24);
			WriteInt32LE(bmp, 30, 0);
			WriteInt32LE(bmp, 34, pixelDataSize);
			WriteInt32LE(bmp, 38, 2835);
			WriteInt32LE(bmp, 42, 2835);
			WriteInt32LE(bmp, 46, 0);
			WriteInt32LE(bmp, 50, 0);

			int pixelBase = 54;

			for (int py = 0; py < heightPixels; py++)
			{
				int logicalY = heightPixels - 1 - py;
				int moduleY = (logicalY / scale) - border;

				int rowOffset = pixelBase + py * paddedRowStride;

				for (int px = 0; px < widthPixels; px++)
				{
					int moduleX = (px / scale) - border;

					bool isDark =
						moduleX >= 0 &&
						moduleX < moduleSize &&
						moduleY >= 0 &&
						moduleY < moduleSize &&
						modules[moduleX, moduleY];

					Rgb24 color = isDark ? darkColor : lightColor;

					int offset = rowOffset + px * 3;
					bmp[offset + 0] = color.B;
					bmp[offset + 1] = color.G;
					bmp[offset + 2] = color.R;
				}
			}

			return bmp;
		}

		private static void WriteInt16LE(byte[] buffer, int offset, int value)
		{
			buffer[offset + 0] = (byte)(value & 0xFF);
			buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
		}

		private static void WriteInt32LE(byte[] buffer, int offset, int value)
		{
			buffer[offset + 0] = (byte)(value & 0xFF);
			buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
			buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
			buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
		}
	}

	// =========================================================================
	// Tables
	// =========================================================================

	internal static class QrTables
	{
		// Index mapping:
		// 0 = Low
		// 1 = Medium
		// 2 = Quartile
		// 3 = High

		private static readonly int[][] EccCodewordsPerBlockTable = new[]
		{
			new[]
			{
				-1,
				7, 10, 15, 20, 26, 18, 20, 24, 30, 18,
				20, 24, 26, 30, 22, 24, 28, 30, 28, 28,
				28, 28, 30, 30, 26, 28, 30, 30, 30, 30,
				30, 30, 30, 30, 30, 30, 30, 30, 30, 30
			},
			new[]
			{
				-1,
				10, 16, 26, 18, 24, 16, 18, 22, 22, 26,
				30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
				26, 28, 28, 28, 28, 28, 28, 28, 28, 28,
				28, 28, 28, 28, 28, 28, 28, 28, 28, 28
			},
			new[]
			{
				-1,
				13, 22, 18, 26, 18, 24, 18, 22, 20, 24,
				28, 26, 24, 20, 30, 24, 28, 28, 26, 30,
				28, 30, 30, 30, 30, 28, 30, 30, 30, 30,
				30, 30, 30, 30, 30, 30, 30, 30, 30, 30
			},
			new[]
			{
				-1,
				17, 28, 22, 16, 22, 28, 26, 26, 24, 28,
				24, 28, 22, 24, 24, 30, 28, 28, 26, 28,
				30, 24, 30, 30, 30, 30, 30, 30, 30, 30,
				30, 30, 30, 30, 30, 30, 30, 30, 30, 30
			}
		};

		private static readonly int[][] NumErrorCorrectionBlocksTable = new[]
		{
			new[]
			{
				-1,
				1, 1, 1, 1, 1, 2, 2, 2, 2, 4,
				4, 4, 4, 4, 6, 6, 6, 6, 7, 8,
				8, 9, 9, 10, 12, 12, 12, 13, 14, 15,
				16, 17, 18, 19, 19, 20, 21, 22, 24, 25
			},
			new[]
			{
				-1,
				1, 1, 1, 2, 2, 4, 4, 4, 5, 5,
				5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
				17, 17, 18, 20, 21, 23, 25, 26, 28, 29,
				31, 33, 35, 37, 38, 40, 43, 45, 47, 49
			},
			new[]
			{
				-1,
				1, 1, 2, 2, 4, 4, 6, 6, 8, 8,
				8, 10, 12, 16, 12, 17, 16, 18, 21, 20,
				23, 23, 25, 27, 29, 34, 34, 35, 38, 40,
				43, 45, 48, 51, 53, 56, 59, 62, 65, 68
			},
			new[]
			{
				-1,
				1, 1, 2, 4, 4, 4, 5, 6, 8, 8,
				11, 11, 16, 16, 18, 16, 19, 21, 25, 25,
				25, 34, 30, 32, 35, 37, 40, 42, 45, 48,
				51, 54, 57, 60, 63, 66, 70, 74, 77, 81
			}
		};

		public static int GetNumRawDataModules(int version)
		{
			Guard.VersionRange(version, nameof(version));

			int result = (16 * version + 128) * version + 64;

			if (version >= 2)
			{
				int numAlign = version / 7 + 2;
				result -= (25 * numAlign - 10) * numAlign - 55;

				if (version >= 7)
					result -= 36;
			}

			return result;
		}

		public static int GetNumDataCodewords(int version, QrErrorCorrectionLevel ecl)
		{
			int raw = GetNumRawDataModules(version) / 8;
			int ecc = GetEccCodewordsPerBlock(version, ecl);
			int blocks = GetNumErrorCorrectionBlocks(version, ecl);
			return raw - ecc * blocks;
		}

		public static int GetNumErrorCorrectionBlocks(int version, QrErrorCorrectionLevel ecl)
		{
			Guard.VersionRange(version, nameof(version));
			return NumErrorCorrectionBlocksTable[EclIndex(ecl)][version];
		}

		public static int GetEccCodewordsPerBlock(int version, QrErrorCorrectionLevel ecl)
		{
			Guard.VersionRange(version, nameof(version));
			return EccCodewordsPerBlockTable[EclIndex(ecl)][version];
		}

		public static int GetFormatBitsForEcl(QrErrorCorrectionLevel ecl)
		{
			return ecl switch
			{
				QrErrorCorrectionLevel.Low => 1,
				QrErrorCorrectionLevel.Medium => 0,
				QrErrorCorrectionLevel.Quartile => 3,
				QrErrorCorrectionLevel.High => 2,
				_ => throw new InvalidOperationException("Unsupported ECC level.")
			};
		}

		public static int[] GetAlignmentPatternPositions(int version)
		{
			Guard.VersionRange(version, nameof(version));

			if (version == 1)
				return Array.Empty<int>();

			int numAlign = version / 7 + 2;
			int step = version == 32
				? 26
				: ((version * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2)) * 2;

			int[] result = new int[numAlign];
			result[0] = 6;

			for (int i = numAlign - 1, pos = version * 4 + 10; i >= 1; i--, pos -= step)
			{
				result[i] = pos;
			}

			return result;
		}

		private static int EclIndex(QrErrorCorrectionLevel ecl)
		{
			return ecl switch
			{
				QrErrorCorrectionLevel.Low => 0,
				QrErrorCorrectionLevel.Medium => 1,
				QrErrorCorrectionLevel.Quartile => 2,
				QrErrorCorrectionLevel.High => 3,
				_ => throw new InvalidOperationException("Unsupported ECC level.")
			};
		}
	}

	// =========================================================================
	// Bit helpers
	// =========================================================================

	internal static class BitHelper
	{
		public static bool GetBit(byte[] data, int bitIndex)
		{
			int byteIndex = bitIndex >> 3;
			int bitInByte = 7 - (bitIndex & 7);
			return ((data[byteIndex] >> bitInByte) & 1) != 0;
		}

		public static bool GetBit(int value, int bitIndex)
		{
			return ((value >> bitIndex) & 1) != 0;
		}
	}

	// =========================================================================
	// Guard helpers
	// =========================================================================

	internal static class Guard
	{
		public static void NotNull(object? value, string paramName)
		{
			if (value is null)
				throw new ArgumentNullException(paramName);
		}

		public static void NotNullOrWhiteSpace(string? value, string paramName)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException("Value cannot be null or whitespace.", paramName);
		}

		public static void Positive(int value, string paramName)
		{
			if (value <= 0)
				throw new ArgumentOutOfRangeException(paramName, "Value must be > 0.");
		}

		public static void VersionRange(int version, string paramName)
		{
			if (version < 1 || version > 40)
				throw new ArgumentOutOfRangeException(paramName, "QR version must be between 1 and 40.");
		}

		public static void HttpOrHttpsUrl(string value, string paramName)
		{
			if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
				throw new ArgumentException("Value is not a valid absolute URL.", paramName);

			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
				throw new ArgumentException("URL must use http or https.", paramName);
		}
	}
}