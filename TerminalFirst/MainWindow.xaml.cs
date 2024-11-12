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
        public void StatusButton_Click(object sender, RoutedEventArgs e) {
            string command = "GetStatus";
            string data = "{}";
            string checksum = CalculateChecksum(command, data);

            var request = new
            {
                Command = command,
                Data = new { },
                Checksum = checksum
            };
            string jsonRequest = JsonConvert.SerializeObject(request);
            SetCommand(jsonRequest);
        }
        private void SerialButton_Click(object sender, RoutedEventArgs e)
        {
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
            SetCommand(jsonRequest);
        }
        private void SettlementButton_Click(object sender, RoutedEventArgs e)
        {
            Settlement st = new Settlement();
            st.Show();
            this.Close();
        }
        private void ReadCardBtn_Click(object sender, RoutedEventArgs e)
        {
            ReadCard readCard = new ReadCard(); 
            readCard.Show();
            this.Close();
        }
        private void GetLastTransBtn_Click(object sender, RoutedEventArgs e)
        {
            LastTrans lt = new LastTrans();
            lt.Show();
            this.Close();  
        }
        private async void SetCommand(string jsonRequest)
        {
            string response = await SendCommandToTerminal(jsonRequest);
            StatusTextBlock.Text = response;
        }

        public string CalculateChecksum(string command, string data)
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
                return hashString.ToString();
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

                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                    if (responseObject != null) {
                        if (responseObject.Command.ToString() == "GetStatus")
                        {
                            string cardPresent = responseObject.Data.CardPresent.ToString();
                            string stringCardPresent = "";
                            if (cardPresent == "0")
                            {
                                stringCardPresent = "Card Not Present";
                            }
                            string stage = responseObject.Data.Stage.ToString();
                            string stringStage = "";
                            if (stage == "0")
                                stringStage = "Idle";
                            else if (stage == "10")
                                stringStage = "Waiting for card";
                            else if (stage == "20")
                                stringStage = "Chip card reading";
                            else if (stage == "21")
                                stringStage = "Contactless card reading";
                            else if (stage == "40")
                                stringStage = "PIN entry";
                            else if (stage == "50")
                                stringStage = "Bank Host authentication";
                            else if (stage == "60")
                                stringStage = "See Phone";
                            else if (stage == "70")
                                stringStage = "Tap again";
                            else if (stage == "71")
                                stringStage = "Present 1 card";
                            else if (stage == "90")
                                stringStage = "Busy";

                            return $"Card Present: {stringCardPresent}\nStage: {stringStage}";
                        }
                        else if (responseObject.Command.ToString() == "GetSerialNo")
                        {
                            return "SerialNumber: " + responseObject.Data.SerialNo.ToString();
                        }
                        else if (responseObject.Command.ToString() == "Settlement")
                        {
                            string result = responseObject.Result.ToString();
                            if (result == "ACK")
                            {
                                return "Acknowledge";
                            }
                            else if (result == "NAK")
                            {
                                return "Not Acknowledge";
                            }
                            else if (result == "ESC")
                            {
                                return "Invalid Parameter";
                            }
                            else if (result == "CAN")
                            {
                                return "Cancel";
                            }
                            else
                            {
                                return "Error";
                            }

                        }
                        else
                        {
                            return "Wrong Command";
                        }
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

        private void SaleBtnBtn_Click(object sender, RoutedEventArgs e)
        {
            Sale s = new Sale();
            s.Show();
            this.Close();
        }
    }
}
