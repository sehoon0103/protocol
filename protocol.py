from dataclasses import dataclass
from typing import Optional

g_slave_id = 0x01
g_last_slave_id = 0x00  # 마지막 송신 slave id 저장

@dataclass
class ProtocolData:
    slave_id: int
    command: str  # 'W' or 'R'
    address: int
    data: int = 0  # 데이터 없는 경우 0

# --- 유틸리티 함수 ---

def hex_char_to_val(c: str) -> int:
    if '0' <= c <= '9':
        return ord(c) - ord('0')
    if 'A' <= c <= 'F':
        return ord(c) - ord('A') + 10
    return -1  # 소문자나 유효하지 않은 문자

def hex_ascii_to_byte(s: str) -> int:
    hi = hex_char_to_val(s[0])
    lo = hex_char_to_val(s[1])
    if hi < 0 or lo < 0:
        return 0
    return (hi << 4) | lo

def byte_to_hex_ascii(val: int) -> str:
    hex_chars = "0123456789ABCDEF"
    return hex_chars[(val >> 4) & 0x0F] + hex_chars[val & 0x0F]

def calc_checksum_ascii(ascii_str: str) -> int:
    return sum(ord(c) for c in ascii_str) & 0xFF

# --- 프로토콜 프레임 생성 ---

def build_ascii_frame(data: ProtocolData, is_resp: bool) -> str:
    out_buf = []

    out_buf.append(':' if is_resp else '$')
    out_buf.append(byte_to_hex_ascii(data.slave_id))
    out_buf.append(data.command)
    out_buf.append(byte_to_hex_ascii(data.address))

    has_data = False
    if (out_buf[0] == '$' and data.command == 'W') or (out_buf[0] == ':' and data.command == 'R'):
        has_data = True
        out_buf.append(byte_to_hex_ascii(data.data))

    ascii_part = ''.join(out_buf[1:])
    checksum = calc_checksum_ascii(ascii_part)
    out_buf.append(byte_to_hex_ascii(checksum))
    out_buf.append('\n')

    return ''.join(out_buf)

# --- 프로토콜 프레임 파싱 ---

def parse_ascii_frame(buf: str) -> Optional[ProtocolData]:
    if not buf or len(buf) < 9:
        return None
    if buf[0] not in ('$' , ':'):
        return None
    if buf[-1] != '\n':
        return None

    cmd = buf[3]
    if cmd not in ('W', 'R'):
        return None

    has_data = False
    length = len(buf)
    if (buf[0] == '$' and cmd == 'W' and length == 11) or (buf[0] == ':' and cmd == 'R' and length == 11):
        has_data = True
    elif (buf[0] == '$' and cmd == 'R' and length == 9) or (buf[0] == ':' and cmd == 'W' and length == 9):
        has_data = False
    else:
        return None

    ascii_len = 7 if has_data else 5
    ascii_part = buf[1:1+ascii_len]

    checksum_calc = calc_checksum_ascii(ascii_part)
    checksum_recv = hex_ascii_to_byte(buf[1+ascii_len:1+ascii_len+2])
    if checksum_calc != checksum_recv:
        return None

    slave_id = hex_ascii_to_byte(buf[1:3])
    address = hex_ascii_to_byte(buf[4:6])
    data = hex_ascii_to_byte(buf[6:8]) if has_data else 0

    return ProtocolData(slave_id, cmd, address, data)

# --- Master 함수들 ---

def master_write(slave_id: int, address: int, data: int) -> str:
    global g_last_slave_id
    tx = ProtocolData(slave_id, 'W', address, data)
    frame = build_ascii_frame(tx, False)
    g_last_slave_id = slave_id
    print(f"[Master Send WRITE]: {frame}", end='')
    return frame

def master_read(slave_id: int, address: int) -> str:
    global g_last_slave_id
    tx = ProtocolData(slave_id, 'R', address, 0)
    frame = build_ascii_frame(tx, False)
    g_last_slave_id = slave_id
    print(f"[Master Send READ]: {frame}", end='')
    return frame

def master_receive(frame_in: str) -> Optional[ProtocolData]:
    if not frame_in:
        return None
    pdata = parse_ascii_frame(frame_in)
    if pdata is None:
        print(f"[Master] Invalid or corrupted frame: {frame_in.strip()}")
        return None

    if pdata.slave_id != g_last_slave_id:
        print(f"[Master] Unexpected slave ID: {pdata.slave_id:02X} (expected {g_last_slave_id:02X})")
        return None

    if frame_in[0] != ':':
        print(f"[Master] Invalid start char for response: {frame_in[0]}")
        return None

    print(f"[Master] Received response: CMD={pdata.command}, ID={pdata.slave_id:02X}, ADDR={pdata.address:02X}, DATA={pdata.data:02X}")
    return pdata

# --- Slave 함수 ---

class SlaveDevice:
    def __init__(self, slave_id: int):
        self.slave_id = slave_id
        self.reg = [0] * 256

    def receive_and_respond(self, frame_in: str):
        pdata = parse_ascii_frame(frame_in)
        if pdata is None:
            print(f"[Slave] Invalid frame received: {frame_in.strip()}")
            return None

        if pdata.slave_id != self.slave_id:
            print(f"[Slave] Ignored frame for different ID: {pdata.slave_id:02X}")
            return None

        print(f"[Slave] Received CMD={pdata.command}, ID={pdata.slave_id:02X}, ADDR={pdata.address:02X}, DATA={pdata.data:02X}")

        if pdata.command == 'W':
            self.reg[pdata.address] = pdata.data
            ack = ProtocolData(pdata.slave_id, 'W', pdata.address, 0)
            reply = build_ascii_frame(ack, True)
            print(f"[Slave Reply ACK]: {reply}", end='')
            return reply

        elif pdata.command == 'R':
            data = self.reg[pdata.address]
            resp = ProtocolData(pdata.slave_id, 'R', pdata.address, data)
            reply = build_ascii_frame(resp, True)
            print(f"[Slave Reply READ]: {reply}", end='')
            return reply

# --- main 함수 테스트 ---

def main():
    master_write(0x01, 0x10, 0xAA)
    master_read(0x01, 0x10)

    slave = SlaveDevice(0x01)
    slave.receive_and_respond("$01W10AAEB\n")
    slave.receive_and_respond("$01R1014\n")

    # Master가 슬레이브 응답을 받는 예시
    master_receive(":01W100C\n")
    master_receive(":01R100074\n")

if __name__ == "__main__":
    main()
