using Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProgramsRunningServer
{
    internal class Program
    {
        public static bool keepAwake;
        public static int lastPercent = 0;

        private static void Main()
        {
            keepAwake = false;

            var localPort = Validator.Validate<int>(ConfigurationManager.AppSettings["TargetPort"]);
            var localIpAddress = Validator.Validate<IPAddress>(ConfigurationManager.AppSettings["LocalIpAddress"]);
            var clientIpAddress = Validator.Validate<IPAddress>(ConfigurationManager.AppSettings["TargetIpAddress"]);
            var clientMacAddress = Validator.Validate<string>(ConfigurationManager.AppSettings["TargetMacAddress"]);
            var repeatPeriod = Validator.Validate<int>(ConfigurationManager.AppSettings["TimeoutInMinutes"]);

            var thread = new Thread(() => WakeOnLan(clientMacAddress, clientIpAddress));
            thread.Start();

            while (true)
            {
                var listener = new TcpListener(localIpAddress, localPort);
                listener.Start();

                Console.WriteLine("Listening...");
                var client = listener.AcceptTcpClient();

                try
                {
                    var networkStream = client.GetStream();
                    var bytesToSend = Encoding.ASCII.GetBytes(keepAwake.ToString());

                    Console.WriteLine("Sending : " + keepAwake.ToString());
                    networkStream.Write(bytesToSend, 0, bytesToSend.Length);

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("errors.txt", ex.Message + Environment.NewLine);
                }

                client.Close();
                listener.Stop();
                Thread.Sleep(60000 * repeatPeriod);
            }
        }

        public static void WakeOnLan(string clientMacAddress, IPAddress clientIpAddress)
        {
            var programList = Validator.Validate<List<string>>(ConfigurationManager.AppSettings["ProgramList"]);
            var cpuWakeProgramList = Validator.Validate<List<string>>(ConfigurationManager.AppSettings["CpuWakeProgramList"]);
            var useCpuControl = Validator.Validate<bool>(ConfigurationManager.AppSettings["UseCpuControl"]);
            var cpuLowMaxPercent = Validator.Validate<int>(ConfigurationManager.AppSettings["CpuLowMaxPercent"]);

            while (true)
            {
                if (useCpuControl)
                {
                    if (IsAnyOfProgramsRunning(cpuWakeProgramList))
                    {
                        if (lastPercent != 100)
                        {
                            SetCpuResource(cpuMinimum: true, 5);
                            SetCpuResource(cpuMinimum: false, 100);
                        }
                        lastPercent = 100;
                    }
                    else
                    {
                        if (lastPercent != 100)
                        {
                            SetCpuResource(cpuMinimum: true, cpuLowMaxPercent < 5 ? cpuLowMaxPercent : 5);
                            SetCpuResource(cpuMinimum: false, cpuLowMaxPercent);
                        }
                        lastPercent = cpuLowMaxPercent;
                    }
                }

                keepAwake = IsAnyOfProgramsRunning(programList);

                if (!keepAwake)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                Console.WriteLine("Sending Wake On Lan packet");

                var payloadIndex = 0;
                var payload = new byte[1024];

                for (var i = 0; i < 6; i++)
                {
                    payload[payloadIndex] = 255;
                    payloadIndex++;
                }

                clientMacAddress = Regex.Replace(clientMacAddress, "[-|:]", "");

                for (var j = 0; j < 16; j++)
                {
                    for (var k = 0; k < clientMacAddress.Length; k += 2)
                    {
                        var s = clientMacAddress.Substring(k, 2);
                        payload[payloadIndex] = byte.Parse(s, NumberStyles.HexNumber);
                        payloadIndex++;
                    }
                }

                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                {
                    EnableBroadcast = true
                };

                sock.SendTo(payload, new IPEndPoint(clientIpAddress, 0));
                sock.Close(5000);

                Thread.Sleep(5000);
            }
        }

        public static bool IsAnyOfProgramsRunning(List<string> programList)
        {
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                if (programList.Contains(process.ProcessName))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetCpuResource(bool cpuMinimum, int percent)
        {
            if (percent > 100 || percent <= 0)
            {
                percent = 100;
            }

            var procthrottle = "PROCTHROTTLEMAX";

            if (cpuMinimum)
            {
                procthrottle = "PROCTHROTTLEMIN";
            }

            var startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = "cmd.exe",
                Arguments = $"/C \"powercfg.exe -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR {procthrottle} {percent}\""
            };

            var process = new Process { StartInfo = startInfo };
            process.Start();

             startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = "cmd.exe",
                Arguments = $"/C \"powercfg.exe /setactive SCHEME_CURRENT\""
            };

            process = new Process { StartInfo = startInfo };
            process.Start();
        }
    }
}
