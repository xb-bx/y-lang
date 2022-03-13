fn u64(x: i32): u64 
{
    asm 
    {
        mov rax, qword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn u8(x: i32) : u8
{
    asm 
    {
        mov rax, qword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn char(x: i32): char 
{
    asm 
    {
        mov rax, qword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn char(x: u8): char 
{   
    asm 
    {
        mov rax, qword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
