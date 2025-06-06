using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

class AresHardwareHacking
{
    static SerialPort _serialPort;
    static string ps1Prompt = string.Empty;
    static List<string> history = new List<string>();
    static int historyIndex = -1; // Index to track current position in history

    static string banner = @"  ____         ____
 / / /_______ / __/  ___ ________  __ _____
/_  _/ __/ -_)__ \  / _ `/ __/ _ \/ // / _ \
 /_//_/  \__/____/  \_, /_/  \___/\_,_/ .__/
                   /___/             /_/
              4re5 group
          all rights reserved
";

    static void Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine(banner);

        // Setup the Ctrl+C handler
        Console.CancelKeyPress += (sender, e) =>
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Write(new byte[] { 0x03 }, 0, 1); // Send Ctrl+C
                Console.WriteLine("\nSent Ctrl+C to the serial port.");
            }
            e.Cancel = true; // Prevent the application from terminating
        };

        // List available serial ports
        string[] ports = SerialPort.GetPortNames();
        Console.WriteLine("Available Ports:");
        for (int i = 0; i < ports.Length; i++)
        {
            Console.WriteLine((i + 1).ToString() + ": " + ports[i]);
        }

        // Prompt user to select a port
        Console.Write("Select a port by entering its number: ");
        int portIndex = int.Parse(Console.ReadLine()) - 1;
        if (portIndex < 0 || portIndex >= ports.Length)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        // Prompt user to select a baud rate
        int[] baudRates = { 9600, 19200, 38400, 57600, 115200 };
        Console.WriteLine("Available Baud Rates:");
        for (int i = 0; i < baudRates.Length; i++)
        {
            Console.WriteLine((i + 1).ToString() + ": " + baudRates[i]);
        }

        Console.Write("Select a baud rate by entering its number: ");
        int baudRateIndex = int.Parse(Console.ReadLine()) - 1;
        if (baudRateIndex < 0 || baudRateIndex >= baudRates.Length)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        // Configure the serial port
        _serialPort = new SerialPort(ports[portIndex], baudRates[baudRateIndex], Parity.None, 8, StopBits.One);
        _serialPort.DataReceived += SerialPort_DataReceived;

        try
        {
            _serialPort.Open();
            Console.WriteLine("Connected to " + _serialPort.PortName + " at " + _serialPort.BaudRate + " baud.");
            Console.WriteLine("Type a command to send it, or 'exit' to quit.");

            StringBuilder userInput = new StringBuilder();
            int cursorPosition = 0;

            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    string input = userInput.ToString();
                    if (input.ToLower() == "exit")
                        break;
                    else if (input.ToLower() == "about")
                    {
                        Console.Clear();
                        Console.WriteLine(banner + @"
This is a simple serial port communication tool for Windows.
It allows you to connect to a serial port, send commands, and receive responses.
Press any key to continue...");
                        Console.ReadKey();
                        Console.Clear();
                        Console.WriteLine("Type a command to send it, or 'exit' to quit.");
                        userInput.Clear();
                        cursorPosition = 0;
                        continue;
                    }

                    _serialPort.WriteLine(input);
                    history.Add(input); // Add to history
                    historyIndex = history.Count; // Reset history index to the end
                    userInput.Clear();
                    cursorPosition = 0;
                    Console.WriteLine(); // Move to the next line after sending
                }
                else if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    if (history.Count > 0 && historyIndex > 0)
                    {
                        historyIndex--;
                        userInput.Clear();
                        userInput.Append(history[historyIndex]);
                        cursorPosition = userInput.Length;
                        RedrawInputLine(userInput, cursorPosition);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    if (history.Count > 0 && historyIndex < history.Count - 1)
                    {
                        historyIndex++;
                        userInput.Clear();
                        userInput.Append(history[historyIndex]);
                        cursorPosition = userInput.Length;
                        RedrawInputLine(userInput, cursorPosition);
                    }
                    else if (historyIndex == history.Count - 1)
                    {
                        historyIndex++;
                        userInput.Clear();
                        cursorPosition = 0;
                        RedrawInputLine(userInput, cursorPosition);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Backspace && cursorPosition > 0)
                {
                    userInput.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    RedrawInputLine(userInput, cursorPosition);
                }
                else if (keyInfo.Key == ConsoleKey.Delete && cursorPosition < userInput.Length)
                {
                    userInput.Remove(cursorPosition, 1);
                    RedrawInputLine(userInput, cursorPosition);
                }
                else if (keyInfo.Key == ConsoleKey.LeftArrow && cursorPosition > 0)
                {
                    cursorPosition--;
                    Console.CursorLeft = ps1Prompt.Length + cursorPosition;
                }
                else if (keyInfo.Key == ConsoleKey.RightArrow && cursorPosition < userInput.Length)
                {
                    cursorPosition++;
                    Console.CursorLeft = ps1Prompt.Length + cursorPosition;
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    userInput.Insert(cursorPosition, keyInfo.KeyChar);
                    cursorPosition++;
                    RedrawInputLine(userInput, cursorPosition);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
        finally
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
    }

    private static void RedrawInputLine(StringBuilder input, int cursorPosition, bool tab = false)
    {
        if (tab)
            Console.CursorTop = Console.CursorTop - 1; // Move cursor up to redraw

        Console.CursorLeft = 0;
        Console.Write(ps1Prompt + input.ToString() + new string(' ', Console.WindowWidth - ps1Prompt.Length - input.Length));
        Console.CursorLeft = ps1Prompt.Length + cursorPosition;
    }

    private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        string indata = sp.ReadExisting();

        // Detect PS1 prompt
        if (indata.Contains("$") || indata.Contains("#") || indata.Contains(">"))
        {
            int lastNewLineIndex = indata.LastIndexOf('\n');
            if (lastNewLineIndex != -1)
            {
                ps1Prompt = indata.Substring(lastNewLineIndex + 1);
            }
        }
        Console.Write(indata);
    }
}
