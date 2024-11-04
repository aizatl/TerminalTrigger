using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Newtonsoft.Json;

namespace TerminalFirst
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            //string command = "GetStatus";
            string command = "GetSerialNo";
            string data = "{}";
            string checksum = CalculateChecksum(command, data);

            var request = new
            {
                Command = command,
                Data = new { },
                Checksum = checksum
            };

            string jsonRequest = JsonConvert.SerializeObject(request);

            string response = await SendCommandToTerminal(jsonRequest);
            StatusTextBlock.Text = $"Status: {response}";
        }

        private string CalculateChecksum(string command, string data)
        {
            using (MD5 md5 = MD5.Create())
            {
                string rawInput = command + data;
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(rawInput));
                StringBuilder hashString = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    hashString.Append(b.ToString("x2"));
                }
                return hashString.ToString();//
                //test branch 1
            }
        }

        private async System.Threading.Tasks.Task<string> SendCommandToTerminal(string jsonRequest)
        {
            string host = "192.168.100.19";
            int port = 9000;

            try
            {
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] requestBytes = Encoding.UTF8.GetBytes(jsonRequest);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    byte[] responseBuffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                    string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                    MessageBox.Show(jsonResponse);


                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                    if (responseObject != null && responseObject.Data.SerialNo != null)
                    {
                        return responseObject.Data.SerialNo.ToString();
                    }
                    else
                    {
                        return "No result in response or response is invalid.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private void SerialButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
