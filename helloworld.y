fn main()
{
    let a: *i32 = null;
    a[100] = 10;
    let b = *a;

    writestr("a");
}
