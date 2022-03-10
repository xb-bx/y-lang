fn main(): void 
{
    writeascii(72);    
    writeascii(101);   
    writeascii(108);   
    writeascii(108);   
    writeascii(111);   
    writeascii(44);   
    writeascii(32);   
    writeascii(87);   
    writeascii(111);   
    writeascii(114);   
    writeascii(108);   
    writeascii(100);
    writeascii(33);      
}
fn writeascii(x:i64) : void 
{
    asm 
    {
        sub rsp, 1
        mov al, byte[rbp + 16]
        mov byte[rsp], al
        invoke GetStdHandle, STD_OUTPUT_HANDLE
        mov r14, rax
        mov r15, rsp
        invoke WriteConsoleA, r14, r15, 1, 0
    }
}
fn twice(x: i64): i64
{
    ret x * 2;
}
