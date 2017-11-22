﻿using BizHawk.Common.NumberExtensions;
using BizHawk.Emulation.Common;
using System;
using System.Collections.Generic;

namespace BizHawk.Emulation.Cores.Nintendo.GBHawk
{
	public partial class GBHawk : IEmulator, IVideoProvider
	{
		public IEmulatorServiceProvider ServiceProvider { get; }

		public ControllerDefinition ControllerDefinition => _controllerDeck.Definition;

		public byte controller_state;
		public byte controller_state_old;
		public bool in_vblank_old;
		public bool in_vblank;
		public bool vblank_rise;
		bool contr_check_once;

		public void FrameAdvance(IController controller, bool render, bool rendersound)
		{
			//Console.WriteLine("-----------------------FRAME-----------------------");

			if (_tracer.Enabled)
			{
				cpu.TraceCallback = s => _tracer.Put(s);
			}
			else
			{
				cpu.TraceCallback = null;
			}

			_frame++;

			if (controller.IsPressed("Power"))
			{
				// it seems that theMachine.Reset() doesn't clear ram, etc
				// this should leave hsram intact but clear most other things
				HardReset();
			}

			_islag = true;

			GetControllerState(controller);

			do_frame();

			if (_islag)
			{
				_lagcount++;
			}
		}

		public void do_frame()
		{
			// gameboy frames can be variable lengths
			// we want to end a frame when VBlank turns from false to true
			int ticker = 0;
			contr_check_once = true;
			while (!vblank_rise && (ticker < 100000))
			{
				audio.tick();
				timer.tick_1();
				ppu.tick();
				serialport.serial_transfer_tick();

				cpu.ExecuteOne(ref REG_FF0F, REG_FFFF);

				timer.tick_2();

				// check for controller interrupts somewhere around the middle of the frame
				if ((cpu.LY == 50) && contr_check_once)
				{
					byte contr_prev = input_register;

					input_register &= 0xF0;
					if ((input_register & 0x30) == 0x20)
					{
						input_register |= (byte)(controller_state & 0xF);
					}
					else if ((input_register & 0x30) == 0x10)
					{
						input_register |= (byte)((controller_state & 0xF0) >> 4);
					}
					else if ((input_register & 0x30) == 0x00)
					{
						// if both polls are set, then a bit is zero if either or both pins are zero
						byte temp = (byte)((controller_state & 0xF) & ((controller_state & 0xF0) >> 4));
						input_register |= temp;
					}
					else
					{
						input_register |= 0xF;
					}

					// check for interrupts
					if (((contr_prev & 8) > 0) && ((input_register & 8) == 0) ||
						((contr_prev & 4) > 0) && ((input_register & 4) == 0) ||
						((contr_prev & 2) > 0) && ((input_register & 2) == 0) ||
						((contr_prev & 2) > 0) && ((input_register & 1) == 0))
					{
						if (REG_FFFF.Bit(4)) { cpu.FlagI = true; }
						REG_FF0F |= 0x10;
					}
				}


				if (in_vblank && !in_vblank_old)
				{
					vblank_rise = true;
				}
				ticker++;
				in_vblank_old = in_vblank;
			}

			vblank_rise = false;
		}

		public void RunCPUCycle()
		{
			
		}

		public void GetControllerState(IController controller)
		{
			InputCallbacks.Call();
			controller_state = _controllerDeck.ReadPort1(controller);

			// set interrupt flag if a pin went from high to low
			if (controller_state < controller_state_old)
			{
				if (REG_FFFF.Bit(4)) { cpu.FlagI = true; }			
				REG_FF0F |= 0x10; 
			}

			controller_state_old = controller_state;
		}

		public int Frame => _frame;

		public string SystemId => "GB"; 

		public bool DeterministicEmulation { get; set; }

		public void ResetCounters()
		{
			_frame = 0;
			_lagcount = 0;
			_islag = false;
		}

		public CoreComm CoreComm { get; }

		public void Dispose()
		{

		}


		#region Video provider

		public int _frameHz = 60;

		public int[] _vidbuffer;

		public int[] GetVideoBuffer()
		{
			return _vidbuffer;
		}

		public int VirtualWidth => 160;
		public int VirtualHeight => 144;
		public int BufferWidth => 160;
		public int BufferHeight => 144;
		public int BackgroundColor => unchecked((int)0xFF000000);
		public int VsyncNumerator => _frameHz;
		public int VsyncDenominator => 1;

		public static readonly uint[] color_palette = { 0xFFFFFFFF , 0xFFAAAAAA, 0xFF555555, 0xFF000000 };

		#endregion
	}
}