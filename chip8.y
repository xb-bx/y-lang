include "sdl2_api.y";
let seed = 0;
dllimport "kernel32.dll" 
{
    extern fn CreateFileA(file: *char, access: u32, share_mode: u32, attr: *void, create_disp: u32, flags: u32, template: *void): *void;
    extern fn GetFileSize(file: *void, high: *u32): u32;
    extern fn ReadFile(file: *void, buff: *u8, to_read: u32, read: *u32, overlapped: *void);
} 


fn load_file(file: *char, size: *u64): *u8 
{
    let fileh = CreateFileA(file, 0b10000000000000000000000000000000, 0, null, 3, 128, null);
    *size = cast(GetFileSize(fileh, null), u64);
    let buff = new_array(size, typeof(u8)).data;
    let read: u32 = 0;
    ReadFile(fileh, buff, cast(*size, u32), &read, null);
    ret buff;
} 
struct OpCode 
{
    first: u8;
    NNN: u16;
    full: u16;
    NN: u8;
    x: u8;
    y: u8;
    n: u8;
    constructor(instr: u16) 
    {
        this.full = instr;
        this.first = cast((instr & 0xF000) >> 12, u8);
        this.NNN = instr & 0x0fff;
        this.NN = cast(instr & 0x00ff, u8);
        this.n = cast(instr & 0x000f, u8);
        this.x = cast((instr & 0x0f00) >> 8, u8);
        this.y = cast((instr & 0x00f0) >> 4, u8);
        
    }
    fn print() 
    {
        writestr("OPCODE: Full = ");
        writehex(this.full);
        writestr(" x = ");
        writenum(this.x);
        writestr(" y = ");
        writenum(this.y);
        writestr(" n = ");
        writenum(this.n);
        writestr(" nn = ");
        writenum(this.NN);
        writestr(" nnn = ");
        writenum(this.NNN);
        writestr("\r\n");
    }
}
fn writenum(x: u8) 
{
    writenum(cast(x, i32));
}
fn writenum(x: u16)
{
    writenum(cast(x, i32));
}
fn writehex(x: u16) 
{
    if x == 0 { writestr("0"); ret; }
    let alph = cast("0123456789ABCDEF", *u8);
    let buff = new_array(32, typeof(u8)).data;
    let i: u64 = 0;
    while x != 0 
    {
        buff[i] = alph[cast(x & 0xF, u64)];
        i = i + 1;
        x = x >> 4;
    }
    let secbuff = new_array(32, typeof(u8)).data;
    let i1: u64 = 0;
    while i > 0 
    {
        i = i - 1;
        secbuff[i1] = buff[i];
        i1 = i1 + 1;
    }
    secbuff[i1] = 0;
    writestr(cast(secbuff, *char));
}
fn nextrnd() : i32
{
    let rnd = 0;
    asm 
    {
        mov edx, 0
        mov ecx, 1103515245
        mov eax, dword[seed]
        mul ecx 
        add eax, 12345
        mov dword[seed], eax
        mov edx, 0
        mov ecx, 65536 
        div ecx
        mov edx, 0
        mov ecx, 32768
        div ecx
        mov dword[rbp - 8], edx
    }
    ret rnd;
}
struct CPU 
{
    memory: *u8;
    registers: *u8;
    stack: *u16;
    stack_count: u64;
    dt: u8;
    st: u8;
    s: i32;
    I: u16;
    PC: u64;
    display: *u8;
    keys: *bool;
    waiting: i32;
    constructor()
    {
        this.memory = new_array(4096, typeof(u8)).data;
        this.registers = new_array(16, typeof(u8)).data;
        this.stack = cast(new_array(16, typeof(u16)).data, *u16);
        this.stack_count = 0;
        this.display = new_array(64 * 32, typeof(u8)).data;
        this.keys = cast(new_array(16 * typeof(bool).size, typeof(bool)).data, *bool);
        this.dt = 0;
        this.st = 0;
        this.I = 0;
        this.PC = 0x200;
        this.waiting = -1;
        let i: u64 = 0;
        while i < 16 
        {
            this.registers[i] = 0;
            i = i + 1;
        }
    }
    fn clear_display() 
    {
        let size: u64 = 64 * 32;
        let i: u64 = 0;
        while i < size 
        {
            this.display[i] = 0;
            i = i + 1;
        }
    }
    fn clear_keys()
    {
        let i: u64 = 0;
        while i < 16
        {
            this.keys[i] = false;
            i = i + 1;
        }
    }
    fn load_rom(rom: *u8, size: u64) 
    {
        if size >= 4096 - 512 
        {
            writestr("ROM is too large");
            exit();
        }
        writestr("LOAD");
        let i: u64 = 0;
        while i < size 
        {
            this.memory[i + 0x200] = rom[i];
            i = i + 1;
        }
    }
    fn execute_one(input: i32) 
    {
        if this.waiting != -1 
        {
            if input == -1 ret;
            else 
            {
                this.registers[cast(this.waiting, u64)] = cast(input, u8);
                this.waiting = -1;
            }
        }
        if this.dt > 0
        {
            this.dt = this.dt - 1;
        }
        if this.st > 0 this.st = this.st - 1;
        
        if this.PC >= 4096 ret;
        let instr: u16 = cast(this.memory[this.PC], u16) << 8;
        instr = instr | cast(this.memory[this.PC + 1], u16);
        let opcode = new OpCode(instr); 
        this.PC = this.PC + 2;
        if instr == 0x00e0 this.clear_display();
        else if instr == 0x00ee this.return();
        else if opcode.first == 1 this.jump(opcode.NNN);
        else if opcode.first == 2 this.call(opcode.NNN);
        else if opcode.first == 3 this.skip_if_x_is(opcode);
        else if opcode.first == 4 this.skip_if_x_not(opcode);
        else if opcode.first == 5 this.skip_if_x_eq_y(opcode);
        else if opcode.first == 6 this.set_x_kk(opcode);
        else if opcode.first == 7 this.add_x_kk(opcode);
        else if opcode.first == 8 
        {
            if opcode.n == 0 this.copy_x_y(opcode);
            else if opcode.n == 1 this.x_or_y(opcode);
            else if opcode.n == 2 this.x_and_y(opcode);
            else if opcode.n == 3 this.x_xor_y(opcode);
            else if opcode.n == 4 this.x_add_y(opcode);
            else if opcode.n == 5 this.x_sub_y(opcode);
            else if opcode.n == 6 this.x_shr(opcode);
            else if opcode.n == 7 this.y_sub_x(opcode);
            else if opcode.n == 0xE this.x_shl(opcode);
        }
        else if opcode.first == 9 this.skip_if_x_neq_y(opcode);
        else if opcode.first == 0xA this.I = opcode.NNN;
        else if opcode.first == 0xB this.jump_reg(opcode);
        else if opcode.first == 0xC this.rand(opcode);
        else if opcode.first == 0xD this.draw(opcode);
        else if opcode.first == 0xE && opcode.NN == 0x9E this.skip_if_pressed(opcode);
        else if opcode.first == 0xE && opcode.NN == 0xA1 this.skip_if_not_pressed(opcode);
        else if opcode.first == 0xF
        {
            if opcode.NN == 0x29 this.I = cast(this.registers[opcode.x], u16) * 5;
            else if opcode.NN == 0x07 this.load_dt(opcode);
            else if opcode.NN == 0x0A this.waiting = cast(opcode.x, i32);
            else if opcode.NN == 0x15 this.set_dt(opcode);
            else if opcode.NN == 0x18 this.set_st(opcode);
            else if opcode.NN == 0x1E this.add_to_I(opcode);
            else if opcode.NN == 0x33 this.save_bcd(opcode);
            else if opcode.NN == 0x55 this.save_regs(opcode);
            else if opcode.NN == 0x65 this.load_regs(opcode);

        }
        
    }
    fn save_regs(opcode: OpCode)
    {
        let x = cast(opcode.x, u64);
        let i = cast(this.I, u64);
        let c: u64 = 0;
        while c < x 
        {
            this.memory[i + c] = this.registers[c];
            c = c + 1;
        }
    }
    fn load_regs(opcode: OpCode)
    {
        let x = cast(opcode.x, u64);
        let i = cast(this.I, u64);
        let c: u64 = 0;
        while c < x 
        {
            this.registers[c] = this.memory[i + c]; 
            c = c + 1;
        }
    }
    fn save_bcd(opcode: OpCode)
    {
        let x = this.registers[opcode.x];
        let h = x / 100;
        let t = (x - (h * 100)) / 10;
        let o = (x - (h * 100) - (t * 10));
        let i = cast(this.I, u64);
        this.memory[i] = h;
        this.memory[i + 1] = t;
        this.memory[i + 2] = o;
    }
    fn add_to_I(opcode: OpCode)
    {
        let x = this.registers[opcode.x];
        this.I = this.I + x;
    }
    fn load_dt(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = this.dt;
    }

    fn set_dt(opcode: OpCode) 
    {
        let x = this.registers[opcode.x];
        this.dt = x;
    }
    fn set_st(opcode: OpCode) 
    {
        let x = this.registers[opcode.x];
        this.st = x;
    }
    fn skip_if_pressed(opcode: OpCode) 
    {
        let key = this.registers[opcode.x];
        if this.keys[cast(key, i32)] this.PC = this.PC + 2;
    }
    fn skip_if_not_pressed(opcode: OpCode)
    {
        let key = this.registers[opcode.x];
        if this.keys[cast(key, i32)] == false this.PC = this.PC + 2;
    }
    fn show_state() 
    {
        writestr("\r\n");
        writestr("I = ");
        writehex(this.I);
        writestr(" PC = ");
        writenum(this.PC);
        writestr("\r\n");
        let i:u64 = 0;
        while i < 16 
        {
            writestr("V");
            writenum(i);
            writestr("=");
            writenum(this.registers[i]);
            writestr(" ");
            i = i + 1;
        }
        writestr("\r\n");
    }
    fn draw(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64) & 0xff];
        let y = this.registers[cast(opcode.y, u64) & 0xff];
        let n = cast(opcode.n, u64);
        let iy: u64 = 0;
        while iy < n 
        {
            let cy = ((cast(y, u64) & 0xff) + iy) % 32;
            let bits = this.memory[(cast(this.I, u64) & 0xffff) + iy];
            let ix: u64 = 0;
            while ix < 8 
            {
                let cx = ((cast(x, u64) & 0xff) + ix) % 64;
                let bit = (bits >> (7 - cast(ix, u8))) & 1;
                let curr = this.display[(cy * 64) + cx];
                if bit == 1 
                {
                    if curr == 1 
                    {
                        this.display[(cy * 64) + cx] = 0;
                        this.registers[15] = 1;
                    }
                    else 
                    {
                        this.display[(cy * 64) + cx] = 1;
                    }
                }
                ix = ix + 1;
            }
            iy = iy + 1;
        }
    }
    fn rand(opcode: OpCode)
    {
        let rnd = cast(nextrnd(), u8) & opcode.NN;
        this.registers[cast(opcode.x, u64)] = rnd;
    }
    fn jump_reg(opcode: OpCode) 
    {
        let x = this.registers[opcode.x];
        this.PC = this.PC + cast(opcode.NNN + x, u64);
    }
    fn skip_if_x_neq_y(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)];
        let y = this.registers[cast(opcode.y, u64)];
        if x != y this.PC = this.PC + 2;
    }
    fn skip_if_x_is(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)];
        if x == opcode.NN this.PC = this.PC + 2;
    }
    fn x_shl(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)]; 
        this.registers[15] = (x & 0b10000000) >> 7;
        this.registers[cast(opcode.x, u64)] = x << 1;
    }
    fn x_shr(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)]; 
        this.registers[15] = x & 1;
        this.registers[cast(opcode.x, u64)] = x >> 1;
    }
    fn x_add_y(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)]; 
        this.registers[cast(opcode.x, u64)] = x + this.registers[cast(opcode.y, u64)];
        if x > this.registers[cast(opcode.x, u64)] this.registers[15] = 1;
        else this.registers[15] = 0;
    }
    fn y_sub_x(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)];
        let y = this.registers[cast(opcode.y, u64)];
        this.registers[cast(opcode.x, u64)] = y - x;
        if y > x this.registers[15] = 1;
        else this.registers[15] = 0;
    }
    fn x_sub_y(opcode: OpCode) 
    {
        let x = this.registers[cast(opcode.x, u64)];
        let y = this.registers[cast(opcode.y, u64)];
        this.registers[cast(opcode.x, u64)] = x - y;
        if x > y this.registers[15] = 1;
        else this.registers[15] = 0;
    }
    fn x_xor_y(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = this.registers[cast(opcode.x, u64)] ^ this.registers[cast(opcode.y, u64)];
    }
    fn x_or_y(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = this.registers[cast(opcode.x, u64)] | this.registers[cast(opcode.y, u64)];
    }
    fn x_and_y(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = this.registers[cast(opcode.x, u64)] & this.registers[cast(opcode.y, u64)];
    }
    fn copy_x_y(opcode: OpCode)
    {
        this.registers[cast(opcode.x, u64)] = this.registers[cast(opcode.y, u64)];
    }
    fn add_x_kk(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = this.registers[cast(opcode.x, u64)] + opcode.NN;
    }
    fn set_x_kk(opcode: OpCode) 
    {
        this.registers[cast(opcode.x, u64)] = opcode.NN;
    }
    fn skip_if_x_not(opcode: OpCode)
    {
        if this.registers[opcode.x] != opcode.NN this.PC = this.PC + 2;
    }
    fn skip_if_x_eq_y(opcode: OpCode)
    {
        if this.registers[opcode.x] == this.registers[opcode.y] this.PC = this.PC + 2;
    }
    fn call(addr: u16) 
    {
        if addr >= 4096 
        {
            writestr("Address is out of bounds\r\n");
            exit();
        }
        if this.stack_count == 16 
        {
            writestr("CPU STACK OVERFLOW");
            exit();
        }
        this.stack[this.stack_count] = cast(this.PC, u16);
        this.stack_count = this.stack_count + 1;
        this.PC = addr;
    }
    fn jump(addr: u16) 
    {
        if addr >= 4096 
        {
            writestr("Address is out of bounds\r\n");
            exit();
        }
        this.PC = addr;
    }
    fn return() 
    {
        if this.stack_count == 0 
        {
            writestr("\r\nCPU STACK UNDERFLOW\r\n");
            exit();
        }
        this.PC = cast(this.stack[this.stack_count - 1], u64);
        this.stack_count = this.stack_count - 1;
    }
    fn load_font() 
    {
        this.memory[0] = 0xF0;
        this.memory[1] = 0x90;
        this.memory[2] = 0x90;
        this.memory[3] = 0x90;
        this.memory[4] = 0xF0;
        this.memory[5] = 0x20;
        this.memory[6] = 0x60;
        this.memory[7] = 0x20;
        this.memory[8] = 0x20;
        this.memory[9] = 0x70;
        this.memory[10] = 0xF0;
        this.memory[11] = 0x10;
        this.memory[12] = 0xF0;
        this.memory[13] = 0x80;
        this.memory[14] = 0xF0;
        this.memory[15] = 0xF0;
        this.memory[16] = 0x10;
        this.memory[17] = 0xF0;
        this.memory[18] = 0x10;
        this.memory[19] = 0xF0;
        this.memory[20] = 0x90;
        this.memory[21] = 0x90;
        this.memory[22] = 0xF0;
        this.memory[23] = 0x10;
        this.memory[24] = 0x10;
        this.memory[25] = 0xF0;
        this.memory[26] = 0x80;
        this.memory[27] = 0xF0;
        this.memory[28] = 0x10;
        this.memory[29] = 0xF0;
        this.memory[30] = 0xF0;
        this.memory[31] = 0x80;
        this.memory[32] = 0xF0;
        this.memory[33] = 0x90;
        this.memory[34] = 0xF0;
        this.memory[35] = 0xF0;
        this.memory[36] = 0x10;
        this.memory[37] = 0x20;
        this.memory[38] = 0x40;
        this.memory[39] = 0x40;
        this.memory[40] = 0xF0;
        this.memory[41] = 0x90;
        this.memory[42] = 0xF0;
        this.memory[43] = 0x90;
        this.memory[44] = 0xF0;
        this.memory[45] = 0xF0;
        this.memory[46] = 0x90;
        this.memory[47] = 0xF0;
        this.memory[48] = 0x10;
        this.memory[49] = 0xF0;
        this.memory[50] = 0xF0;
        this.memory[51] = 0x90;
        this.memory[52] = 0xF0;
        this.memory[53] = 0x90;
        this.memory[54] = 0x90;
        this.memory[55] = 0xE0;
        this.memory[56] = 0x90;
        this.memory[57] = 0xE0;
        this.memory[58] = 0x90;
        this.memory[59] = 0xE0;
        this.memory[60] = 0xF0;
        this.memory[61] = 0x80;
        this.memory[62] = 0x80;
        this.memory[63] = 0x80;
        this.memory[64] = 0xF0;
        this.memory[65] = 0xE0;
        this.memory[66] = 0x90;
        this.memory[67] = 0x90;
        this.memory[68] = 0x90;
        this.memory[69] = 0xE0;
        this.memory[70] = 0xF0;
        this.memory[71] = 0x80;
        this.memory[72] = 0xF0;
        this.memory[73] = 0x80;
        this.memory[74] = 0xF0;
        this.memory[75] = 0xF0;
        this.memory[76] = 0x80;
        this.memory[77] = 0xF0;
        this.memory[78] = 0x80;
        this.memory[79] = 0x80;
    }
}
fn main() 
{
    set_start();
    let window_width = 640;
    let window_height = 320;
    let event = cast(new_obj(128, typeof(Event)), *Event);
    if SDL_Init(SDL_INIT_VIDEO) < 0 
    {
        writestr(SDL_GetError());
        ret;
    }
    let window = SDL_CreateWindow("w", 100, 100, window_width, window_height, 0);
    if window == null
    {
        writestr(SDL_GetError());
        ret;
    }
    let renderer = SDL_CreateRenderer(window, -1, 0);
    let isQuit = false;
    let size: u64 = 0;
    let rom = load_file("D:/projects/y-lang/rom.ch8", &size);
    let cpu = new CPU();
    cpu.load_rom(rom, size);
    cpu.load_font();
    let input = -1;
    while isQuit == false 
    {
        SDL_PollEvent(event);
        if event.type == EventType.Quit isQuit = true;
        else 
        {
            if event.type == EventType.KeyDown 
            {
         
                let code = event.keyboard.key.scancode; 
                if code == KeyCode.Num1 { cpu.keys[0] = true; input = 0; }
                else if code == KeyCode.Num2 { cpu.keys[1] = true; input = 1; }
                else if code == KeyCode.Num3 { cpu.keys[2] = true; input = 2; }
                else if code == KeyCode.Num4 { cpu.keys[3] = true; input = 3; }
                else if code == KeyCode.Q { cpu.keys[4] = true; input = 4; }
                else if code == KeyCode.W { cpu.keys[5] = true; input = 5; }
                else if code == KeyCode.E { cpu.keys[6] = true; input = 6; }
                else if code == KeyCode.R { cpu.keys[7] = true; input = 7; }
                else if code == KeyCode.A { cpu.keys[8] = true; input = 8; }
                else if code == KeyCode.S { cpu.keys[9] = true; input = 9; }
                else if code == KeyCode.D { cpu.keys[10] = true; input = 10; }
                else if code == KeyCode.F { cpu.keys[11] = true; input = 11; }
                else if code == KeyCode.Z { cpu.keys[12] = true; input = 12; }
                else if code == KeyCode.X { cpu.keys[13] = true; input = 13; }
                else if code == KeyCode.C { cpu.keys[14] = true; input = 14; }
                else if code == KeyCode.V { cpu.keys[15] = true; input = 15; }
                else input = -1;
            }
            else if event.type == EventType.KeyUp 
            {
         
                code = event.keyboard.key.scancode; 
                if code == KeyCode.Num1 { cpu.keys[0] = false; input = 0; }
                else if code == KeyCode.Num2 { cpu.keys[1] = false; input = 1; }
                else if code == KeyCode.Num3 { cpu.keys[2] = false; input = 2; }
                else if code == KeyCode.Num4 { cpu.keys[3] = false; input = 3; }
                else if code == KeyCode.Q { cpu.keys[4] = false; input = 4; }
                else if code == KeyCode.W { cpu.keys[5] = false; input = 5; }
                else if code == KeyCode.E { cpu.keys[6] = false; input = 6; }
                else if code == KeyCode.R { cpu.keys[7] = false; input = 7; }
                else if code == KeyCode.A { cpu.keys[8] = false; input = 8; }
                else if code == KeyCode.S { cpu.keys[9] = false; input = 9; }
                else if code == KeyCode.D { cpu.keys[10] = false; input = 10; }
                else if code == KeyCode.F { cpu.keys[11] = false; input = 11; }
                else if code == KeyCode.Z { cpu.keys[12] = false; input = 12; }
                else if code == KeyCode.X { cpu.keys[13] = false; input = 13; }
                else if code == KeyCode.C { cpu.keys[14] = false; input = 14; }
                else if code == KeyCode.V { cpu.keys[15] = false; input = 15; }
                else input = -1;
            }
            else input = -1;
            cpu.execute_one(input);
            renderer.set_color(new RGBA(0,0,0,255));
            renderer.clear();
            let y: u64 = 0;
            renderer.set_color(new RGBA(255,255, 255, 255));
            while y < 32 
            {
                let x: u64 = 0;
                while x < 64 
                {
                    if cpu.display[(y * 64) + x] == 1 
                    {
                        renderer.fill_rect(new Rect(cast(x * 10, i32), cast(y * 10, i32), 10, 10));
                    }
                    x = x + 1;
                }
                y = y + 1;
            }
            renderer.render_present();
        }
    }
}
