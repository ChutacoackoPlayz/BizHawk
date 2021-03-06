﻿using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Nintendo.Gameboy
{
	[Core(
		"DualGambatte",
		"sinamas/natt",
		isPorted: true,
		isReleased: true)]
	[ServiceNotApplicable(new[] { typeof(IDriveLight) })]
	public partial class GambatteLink : IEmulator, IVideoProvider, ISoundProvider, IInputPollable, ISaveRam, IStatable, ILinkable,
		IBoardInfo, IRomInfo, IDebuggable, ISettable<GambatteLink.GambatteLinkSettings, GambatteLink.GambatteLinkSyncSettings>, ICodeDataLogger
	{
		public GambatteLink(CoreComm comm, GameInfo leftinfo, byte[] leftrom, GameInfo rightinfo, byte[] rightrom, object settings, object syncSettings, bool deterministic)
		{
			ServiceProvider = new BasicServiceProvider(this);
			GambatteLinkSettings linkSettings = (GambatteLinkSettings)settings ?? new GambatteLinkSettings();
			GambatteLinkSyncSettings linkSyncSettings = (GambatteLinkSyncSettings)syncSettings ?? new GambatteLinkSyncSettings();

			L = new Gameboy(comm, leftinfo, leftrom, linkSettings.L, linkSyncSettings.L, deterministic);
			R = new Gameboy(comm, rightinfo, rightrom, linkSettings.R, linkSyncSettings.R, deterministic);

			// connect link cable
			LibGambatte.gambatte_linkstatus(L.GambatteState, 259);
			LibGambatte.gambatte_linkstatus(R.GambatteState, 259);

			L.ConnectInputCallbackSystem(_inputCallbacks);
			R.ConnectInputCallbackSystem(_inputCallbacks);
			L.ConnectMemoryCallbackSystem(_memorycallbacks);
			R.ConnectMemoryCallbackSystem(_memorycallbacks);

			RomDetails = "LEFT:\r\n" + L.RomDetails + "RIGHT:\r\n" + R.RomDetails;

			LinkConnected = true;

			Frame = 0;
			LagCount = 0;
			IsLagFrame = false;

			_blipLeft = new BlipBuffer(1024);
			_blipRight = new BlipBuffer(1024);
			_blipLeft.SetRates(2097152 * 2, 44100);
			_blipRight.SetRates(2097152 * 2, 44100);

			SetMemoryDomains();
		}

		public string RomDetails { get; }

		public bool LinkConnected
		{
			get => _cableconnected;
			set => _cableconnected = value;
		}

		private bool _disposed = false;

		private Gameboy L;
		private Gameboy R;

		// counter to ensure we do 35112 samples per frame
		private int _overflowL = 0;
		private int _overflowR = 0;

		// if true, the link cable is currently connected
		private bool _cableconnected = true;

		// if true, the link cable toggle signal is currently asserted
		private bool _cablediscosignal = false;

		const int SampPerFrame = 35112;

		private readonly SaveController LCont = new SaveController(Gameboy.GbController);
		private readonly SaveController RCont = new SaveController(Gameboy.GbController);

		public bool IsCGBMode(bool right)
		{
			return right ? R.IsCGBMode() : L.IsCGBMode();
		}

		private static readonly ControllerDefinition DualGbController = new ControllerDefinition
		{
			Name = "Dual Gameboy Controller",
			BoolButtons =
			{
				"P1 Up", "P1 Down", "P1 Left", "P1 Right", "P1 A", "P1 B", "P1 Select", "P1 Start", "P1 Power",
				"P2 Up", "P2 Down", "P2 Left", "P2 Right", "P2 A", "P2 B", "P2 Select", "P2 Start", "P2 Power",
				"Toggle Cable"
			}
		};
	}
}
