using Shared;
using System;
using System.Configuration;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ProgramsRunningServer
{
    class Program
    {
        static void Main()
        {
            var remotePort = Validator.Validate<int>(ConfigurationManager.AppSettings["TargetPort"]);
            var remoteIpAddress = Validator.Validate<string>(ConfigurationManager.AppSettings["TargetIpAddress"]);
            var repeatPeriod = Validator.Validate<int>(ConfigurationManager.AppSettings["TimeoutInMinutes"]);

            while (true)
            {
                try
                {
                    var client = new TcpClient(remoteIpAddress, remotePort);
                    var nwStream = client.GetStream();
                    var bytesToRead = new byte[client.ReceiveBufferSize];
                    var bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
                    var reply = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);

                    Console.WriteLine("Received : " + reply);
                    client.Close();

                    if (reply == "True")
                    {
                        NativeMethods.SetThreadExecutionState(EXECUTION_STATE.ES_SYSTEM_REQUIRED);
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText("errors.txt", ex.Message + Environment.NewLine);
                }

                Thread.Sleep(repeatPeriod * 60000);
            }
        }
    }

    public enum EXECUTION_STATE : uint
    {
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    internal class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
    }    
}
