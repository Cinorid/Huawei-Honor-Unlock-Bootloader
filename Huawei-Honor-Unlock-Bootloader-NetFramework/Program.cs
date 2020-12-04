using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Huawei_Honor_Unlock_Bootloader_NetFramework
{
	class Program
	{
		static long staticimei = 0;                 //enter your imei here if you dont want to be asked every start
		static bool quickstart = true;              //set to True to not need to confirm on script start, should be used in combination with staticimei

		static void Main(string[] args)
		{
			// Bruteforce setup:

			Console.WriteLine("\n\n           Unlock Bootloader script - By SkyEmie_\' and programminghoch10");
			Console.WriteLine("\n\n  (You must enable USB DEBUGGING and OEM UNLOCK in the developer options of the target device...)");
			Console.WriteLine("  !!! All data will be erased !!! \n");

			var prc = Process.Start("adb", "devices");
			prc.WaitForExit();

			Console.WriteLine("Please select \"Always allow from this computer\" in the adb dialog!");

			long imei = 0;

			var checksum = 1;
			while (checksum != 0)
			{
				if (staticimei == 0)
				{
					Console.Write("Type IMEI: ");
					imei = Convert.ToInt64(Console.ReadLine());
				}
				else if (staticimei > 0)
				{
					imei = staticimei;
				}

				checksum = luhn_checksum(imei);

				if (checksum != 0)
				{
					Console.WriteLine("IMEI incorrect!");
					if (staticimei > 0)
					{
						Environment.Exit(0);
					}
				}

				var increment = Convert.ToInt64(Math.Sqrt(imei) * 1024);

				if (quickstart == false)
				{
					Console.WriteLine("Press enter to reboot your device...\n");
					Console.ReadKey();
				}

				prc = Process.Start("adb", "reboot bootloader");
				prc.WaitForExit();
				// input('Press enter when your device is ready... (This may take time, depending on your phone)\n');

				var codeOEM = BruteforceBootloader(increment);

				prc = Process.Start("fastboot", "getvar unlocked");
				prc.WaitForExit();

				//prc = Process.Start("fastboot", "reboot");
				//prc.WaitForExit();

				Console.WriteLine("\n\nDevice unlocked! OEM CODE: ' + codeOEM + '\n");
				Environment.Exit(0);
			}
		}

		public static long BruteforceBootloader(long increment)
		{
			var psi = new ProcessStartInfo(@"fastboot")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
			};
			

			FileStream bak;
			var algoOEMcode = 1000000000000000;
			var autoreboot = false;
			var autorebootcount = 4;
			var savecount = 200;
			var unknownfail = true;
			var failmsg = "fail";
			var unlock = false;
			var n = 0;
			while (unlock == false)
			{
				string currentCode = algoOEMcode.ToString().PadLeft(16, '0');
				Console.WriteLine("Bruteforce is running...\nCurrently testing code " + currentCode + "\nProgress: " + Math.Round(algoOEMcode / 10000000000000000 * 100d, 2).ToString() + "%");
				
				psi.Arguments = "oem unlock " + currentCode;
				var proc = Process.Start(psi);
				string output = proc.StandardOutput.ReadToEnd();
				proc.WaitForExit();

				Console.WriteLine(output);
				output = output.ToLower();
				n += 1;

				if (output.Contains("success"))
				{
					bak = File.OpenWrite("unlock_code.txt");
					bak.Write(Encoding.UTF8.GetBytes(currentCode), 0, Encoding.UTF8.GetBytes(currentCode).Length);
					bak.Flush();
					bak.Close();
					Console.WriteLine("Your bruteforce result has been saved in \"unlock_code.txt\"");
					return algoOEMcode;
				}
				if (output.Contains("reboot"))
				{
					Console.WriteLine("Target device has bruteforce protection!");
					Console.WriteLine("Waiting for reboot and trying again...");
					var prc = Process.Start("adb", "wait-for-device");
					prc.WaitForExit();
					prc = Process.Start("adb", "reboot bootloader");
					prc.WaitForExit();
					Console.WriteLine("Device reboot requested, turning on reboot workaround.");
					autoreboot = true;
				}
				if (output.Contains(failmsg))
				{
					//print("Code " + str(algoOEMcode) + " is wrong, trying next one...")
				}
				if (!output.Contains("success") && !output.Contains("reboot") && !output.Contains(failmsg) && unknownfail)
				{
					// fail here to prevent continuing bruteforce on success or another error the script cant handle
					Console.WriteLine("Could not parse output.");
					Console.WriteLine("Please check the output above yourself.");
					Console.WriteLine("If you want to disable this feature, switch variable unknownfail to False");
					Console.ReadKey();
					Environment.Exit(0);
				}
				if (n % savecount == 0)
				{
					bak = File.OpenWrite("unlock_code.txt");
					bak.Write(Encoding.UTF8.GetBytes(currentCode), 0, Encoding.UTF8.GetBytes(currentCode).Length);
					bak.Flush();
					bak.Close();
					Console.WriteLine("Your bruteforce progress has been saved in \"unlock_code.txt\"");
				}
				if (n % autorebootcount == 0 && autoreboot)
				{
					Console.WriteLine("Rebooting to prevent bootloader from rebooting...");
					var prc = Process.Start("fastboot", "reboot bootloader");
					prc.WaitForExit();
				}
				algoOEMcode += increment;
				if (algoOEMcode > 10000000000000000)
				{
					Console.WriteLine("OEM Code not found!\n");
					var prc = Process.Start("fastboot", "reboot");
					prc.WaitForExit();
					Environment.Exit(0);
				}
			}

			return 0;
		}

		public static int luhn_checksum(long imei)
		{
			Func<long, List<int>> digits_of = n =>
			{
				return (from d in n.ToString()
						select Convert.ToInt32(d)).ToList();
			};

			Func<List<int>, List<int>> getOddDigits = list =>
			{
				List<int> res = new List<int>();
				for (int i = 0; i < list.Count; i++)
				{
					if ((i + 1) % 2 == 1)
					{
						res.Add(list[i]);
					}
				}
				
				return res;
			};

			Func<List<int>, List<int>> getEvenDigits = list =>
			{
				List<int> res = new List<int>();
				for (int i = 0; i < list.Count; i++)
				{
					if ((i + 1) % 2 == 0)
					{
						res.Add(list[i]);
					}
				}

				return res;
			};

			var digits = digits_of(imei);
			var oddDigits = getOddDigits(digits);
			var evenDigits = getEvenDigits(digits);
			var checksum = 0;
			checksum += oddDigits.Sum();
			foreach (var i in evenDigits)
			{
				checksum += digits_of(i * 2).Sum();
			}

			return checksum % 10;
		}
	}
}
