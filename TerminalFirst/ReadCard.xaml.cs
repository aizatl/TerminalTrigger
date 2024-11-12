using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TerminalFirst
{
    /// <summary>
    /// Interaction logic for ReadCard.xaml
    /// </summary>
    public partial class ReadCard : Window
    {
        public ReadCard()
        {
            InitializeComponent();
        }

        private void ReadCardBtn_Click(object sender, RoutedEventArgs e)
        {
            ResponseStatusTextBox.Text = "";
            string command = "ReadCard";
            bool emvPan = false;
            string emvPanString = (EmvPANComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (emvPanString == "True")
                emvPan = true;

            var dataObject = new { EmvPAN = emvPan };
            string data = JsonConvert.SerializeObject(dataObject);
            string checksum = CalculateChecksum(command, data);

            var request = new
            {
                Command = command,
                Data = dataObject,
                Checksum = checksum
            };
            string jsonRequest = JsonConvert.SerializeObject(request);
            SetCommand(jsonRequest);
        }
        private async void SetCommand(string jsonRequest)
        {
            string response = await SendCommandToTerminal(jsonRequest);
            ResponseStatusTextBox.Text = response;
            if (response == "Acknowledge")
            {
                string cardDetails = await WaitForCardDetails();
                FullCardResultTextBox.Text = cardDetails;
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
                    if (responseObject != null)
                    {
                        if (responseObject.Command.ToString() == "ReadCard")
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
                        else { 
                            return "Error";
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
        private async Task<string> WaitForCardDetails()
        {
            string host = "192.168.100.19";
            int port = 9000;

            try
            {
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] responseBuffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                    string jsonResponse = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                    var responseObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                    if (responseObject != null && responseObject.Command.ToString() == "ReadCard")
                    {
                        return $"Card Type: {responseObject.Data.CardType.ToString()}\n" +
                             $"Card No:{responseObject.Data.StatusCode.ToString()}\n" +
                             $"Hashed PAN:{responseObject.Data.HashedPAN.ToString()}\n" +
                             $"Card Status:{responseObject.Data.CardStatus.ToString()}\n" +
                             $"Card UID:{responseObject.Data.CardUID.ToString()}\n";
                    }
                    else
                    {
                        return "Error: No response for card details.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
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
                return hashString.ToString();
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mv = new MainWindow();
            mv.Show();
            this.Close();
        }
    }
}
