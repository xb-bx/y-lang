fn u64(x: i32): u64 
{
    asm 
    {
        xor rax, rax
        mov eax, dword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn u64(x: u32): u64 
{
    asm 
    {
        xor rax, rax
        mov eax, dword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}

fn i32(x: u8): i32
{
    asm 
    {
        xor rax, rax
        mov eax, dword[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn i32(x: u64): i32
{
    asm 
    {
        xor rax, rax
        mov eax, dword[rbp + 16]
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
        xor rax, rax
        mov al, byte[rbp + 16]
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
        xor rax, rax
        mov al, byte[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
fn u8(x: char): u8 
{   
    asm 
    {   
        xor rax, rax
        mov al, byte[rbp + 16]
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
        xor rax, rax
        mov al, byte[rbp + 16]
        mov rsp, rbp
        pop rbp
        ret
    }
    ret 0;
}
