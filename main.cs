using System;
using System.Text;

public class ProtocolData
{
    public byte SlaveId; // 슬레이브 장치 식별 번호(0x00~0xFF)
    public char Command; // 'W' or 'R'
    public byte Address; // 레지스터나 메모리의 주소 (0x00~0xFF)
    public byte Data;    // 데이터가 없는 경우 0

    public ProtocolData(byte slaveId, char command, byte address, byte data)
    {
        SlaveId = slaveId;
        Command = command;
        Address = address;
        Data = data;
    }
}

public class AsciiProtocol
{
    // 슬레이브 ID 전역 변수 (슬레이브 측에서 사용)
    public static byte g_slave_id = 0x01;
    public static byte g_last_slave_id = 0x00;

    // ASCII HEX 2자리 -> byte 변환 ('0'~'9','A'~'F'만 지원, 소문자는 오류)
    private static int HexCharToVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';                      
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1; // 소문자나 유효하지 않은 문자는 에러
    }

    // 16진수 ↔ byte 변환
    public static byte HexAsciiToByte(string str, int index) 
    {
        int hi = HexCharToVal(str[index]);
        int lo = HexCharToVal(str[index + 1]);
        if (hi < 0 || lo < 0) return 0;
        return (byte)((hi << 4) | lo);
    }

    // byte ↔ ASCII 
    public static void ByteToHexAscii(byte val, StringBuilder sb) 
    {
        const string hex = "0123456789ABCDEF";
        sb.Append(hex[(val >> 4) & 0x0F]);
        sb.Append(hex[val & 0x0F]); 
    }

    // 체크섬 계산: ASCII 문자의 합 (len 만큼)
    public static byte CalcChecksumAscii(string ascii, int len)
    {
        byte sum = 0;
        for (int i = 0; i < len; i++)
        {
            sum += (byte)ascii[i];
        }
        return sum;
    }

    // 프로토콜 프레임 생성
    public static string BuildAsciiFrame(ProtocolData data, bool isResp)
    {
        var sb = new StringBuilder();

        sb.Append(isResp ? ':' : '$');
        ByteToHexAscii(data.SlaveId, sb);
        sb.Append(data.Command);
        ByteToHexAscii(data.Address, sb);

        bool hasData = false;
        if ((sb[0] == '$' && data.Command == 'W') ||
            (sb[0] == ':' && data.Command == 'R'))
        {
            hasData = true;
            ByteToHexAscii(data.Data, sb);
        }

        int checksumLen = hasData ? 7 : 5;
        string asciiPart = sb.ToString(1, checksumLen);
        byte chk = CalcChecksumAscii(asciiPart, checksumLen);
        ByteToHexAscii(chk, sb);

        sb.Append('\n');

        return sb.ToString();
    }

    // 프로토콜 프레임 파싱
    public static bool ParseAsciiFrame(string buf, out ProtocolData outData)
    {
        outData = null;
        if (string.IsNullOrEmpty(buf)) return false;

        int len = buf.Length;
        if (len < 9) return false;
        if (buf[0] != '$' && buf[0] != ':') return false;
        if (buf[len - 1] != '\n') return false;

        char cmd = buf[3];
        if (cmd != 'W' && cmd != 'R') return false;

        bool hasData = false;
        if ((buf[0] == '$' && cmd == 'W' && len == 11) ||
            (buf[0] == ':' && cmd == 'R' && len == 11))
        {
            hasData = true;
        }
        else if ((buf[0] == '$' && cmd == 'R' && len == 9) ||
                 (buf[0] == ':' && cmd == 'W' && len == 9))
        {
            hasData = false;
        }
        else
        {
            return false;
        }

        int asciiLen = hasData ? 7 : 5;
        string asciiPart = buf.Substring(1, asciiLen);
        byte checksumCalc = CalcChecksumAscii(asciiPart, asciiLen);
        byte checksumRecv = HexAsciiToByte(buf, asciiLen + 1);
        if (checksumCalc != checksumRecv) return false;

        byte slaveId = HexAsciiToByte(buf, 1);
        byte address = HexAsciiToByte(buf, 4);
        byte data = hasData ? HexAsciiToByte(buf, 6) : (byte)0;

        outData = new ProtocolData(slaveId, cmd, address, data);
        return true;
    }

    // Master write 명령 전송 예제 (프레임만 출력, 실제전송x)
    public static void MasterWrite(byte slaveId, byte address, byte data)
    {
        var tx = new ProtocolData(slaveId, 'W', address, data);
        string frame = BuildAsciiFrame(tx, false);
		g_last_slave_id = slaveId;
		
        Console.WriteLine("[Master Send WRITE]: " + frame);
    }

    // Master read 명령 전송 예제 (프레임만 출력, 실제전송x)
    public static void MasterRead(byte slaveId, byte address)
    {
        var tx = new ProtocolData(slaveId, 'R', address, 0);
        string frame = BuildAsciiFrame(tx, false);
        g_last_slave_id = slaveId;

        Console.WriteLine("[Master Send READ]: " + frame);
    }

    // Slave receive & respond 예제 (파싱함수 호출)
    public static void SlaveReceiveAndRespond(string frameIn)
    {
        if (!ParseAsciiFrame(frameIn, out ProtocolData rx))
        {
            Console.WriteLine("[Slave] Invalid frame received: " + frameIn);
            return;
        }

        if (rx.SlaveId != g_slave_id)
        {
            Console.WriteLine("[Slave] Ignored frame for different ID: {0:X2}", rx.SlaveId);
            return;
        }

        Console.WriteLine($"[Slave] Received CMD={rx.Command}, ID={rx.SlaveId:X2}, ADDR={rx.Address:X2}, DATA={rx.Data:X2}");

        // 간단 레지스터 시뮬레이션
        // 256 바이트 레지스터 공간
        // 실제로는 필드나 다른 구조체에 저장할 수도 있음
        byte[] reg = new byte[256];

        if (rx.Command == 'W')
        {
            reg[rx.Address] = rx.Data;
            var ack = new ProtocolData(rx.SlaveId, 'W', rx.Address, 0);
            string reply = BuildAsciiFrame(ack, true);
            Console.WriteLine("[Slave Reply ACK]: " + reply);
        }
        else if (rx.Command == 'R')
        {
            byte data = reg[rx.Address];
            var resp = new ProtocolData(rx.SlaveId, 'R', rx.Address, data);
            string reply = BuildAsciiFrame(resp, true);
            Console.WriteLine("[Slave Reply READ]: " + reply);
        }
    }

    // Master receive 응답 함수 예제
    public static void MasterReceive(string frameIn)
    {
        if (!ParseAsciiFrame(frameIn, out ProtocolData rx))
        {
            Console.WriteLine("[Master] Invalid frame received: " + frameIn);
            return;
        }

        if (g_last_slave_id != rx.SlaveId)
        {
            Console.WriteLine("[Master] Unexpected slave ID: " + frameIn);
            return;
        }

        //활용하는 자리?

        Console.WriteLine($"[Master] Received CMD={rx.Command}, ID={rx.SlaveId:X2}, ADDR={rx.Address:X2}, DATA={rx.Data:X2}");
    }
}

// 테스트용 메인 클래스
public class Program
{
    public static void Main()
    {
        AsciiProtocol.MasterWrite(0x01, 0x10, 0xAA);
        AsciiProtocol.MasterRead(0x01, 0x10);

        AsciiProtocol.SlaveReceiveAndRespond("$01W10AAEB\n");
        AsciiProtocol.SlaveReceiveAndRespond("$01R10C3\n");

        AsciiProtocol.MasterReceive(":01W100C\n");
        AsciiProtocol.MasterReceive(":01R10AAEB\n");
    }
}
