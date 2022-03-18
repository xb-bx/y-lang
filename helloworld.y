include "std/utils.y"; 
fn main()
{ 
    let i = 0;
    while i < 4000 
    {   
        writenum(i);
        writestr("\r\n");
        i = i + 1;
    }
}
