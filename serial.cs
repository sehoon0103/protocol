using System;
using System.IO.Ports;
using System.Text;

class SerialComm
{
    private SerialPort _serialPort;

    public SerialComm(string portName, int baudRate = 9600)
    {
        _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
        _serialPort.Encoding = Encoding.ASCII;
        _serialPort.DataReceived += SerialPort_DataReceived;
    }

    public void Open()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
            Console.WriteLine($"Serial port {_serialPort.PortName} opened.");
        }
    }

    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
            Console.WriteLine($"Serial port {_serialPort.PortName} closed.");
        }
    }

    public void Send(string data)
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Write(data);
            Console.WriteLine($"Sent: {data.Replace("\n", "\\n")}");
        }
        else
        {
            Console.WriteLine("Serial port is not open.");
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string data = _serialPort.ReadExisting();
            Console.WriteLine($"Received: {data.Replace("\n", "\\n")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in DataReceived handler: " + ex.Message);
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        // 현재 사용 가능한 포트 검색
        string[] ports = SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            Console.WriteLine("No serial ports found.");
            return;
        }

        Console.WriteLine("Available serial ports:");
        foreach (var port in ports)
        {
            Console.WriteLine($" - {port}");
        }

        // 첫 번째 포트를 자동 선택
        string portName = ports[0];
        Console.WriteLine($"Using port: {portName}");

        SerialComm serial = new SerialComm(portName, 115200);
        serial.Open();

        serial.Send("$01W10AAEB\n");

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();

        serial.Close();
    }
}
