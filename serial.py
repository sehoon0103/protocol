# pip install pyserial
import serial
import serial.tools.list_ports

def list_serial_ports():
    ports = serial.tools.list_ports.comports()
    return [port.device for port in ports]

def main():
    ports = list_serial_ports()
    if not ports:
        print("No serial ports found.")
        return

    print("Available serial ports:")
    for p in ports:
        print(f" - {p}")

    port_name = ports[0]
    print(f"Using port: {port_name}")

    try:
        ser = serial.Serial(port=port_name, baudrate=115200, timeout=1)
        print(f"Opened {port_name}")

        # 데이터 송신
        send_data = "$01W10AAEB\n"
        ser.write(send_data.encode('ascii'))
        print(f"Sent: {send_data.strip()}")

        # 데이터 수신 (예: 1초 동안 대기 후 읽기)
        received = ser.read(100)  # 최대 100 바이트 읽기
        if received:
            print("Received:", received.decode('ascii', errors='ignore').strip())
        else:
            print("No data received.")

        ser.close()
        print(f"Closed {port_name}")

    except serial.SerialException as e:
        print(f"Serial error: {e}")

if __name__ == "__main__":
    main()
