include "std/gc.y";
include "std/utils.y";
include "std/StringBuilder.y";
include "std/List.y";
fn format_obj(obj: Obj): *char 
{
    let builder = new StringBuilder();
    if streq(obj.type.name, "i32") builder.append(*cast(obj.data, *i32));
    else if streq(obj.type.name, "u64") builder.append(*cast(obj.data, *u64));
    else if streq(obj.type.name, "ptr") && streq(obj.type.underlaying.name, "char") builder.append(*cast(obj.data, **char));
    ret builder.build();
}
fn format(fmt: *char, data: *Obj, count: u64): *char 
{
    let builder = new StringBuilder();
    let i: u64 = 0;
    let len = cast(strlen(fmt), u64);
    let argi: u64 = 0;
    while i < len 
    {
        if fmt[i] == '%'
        {
            i = i + 1;
            if i >= len 
            {
                builder.append('%');
            }
            else if fmt[i] == '%' 
            {
                builder.append('%');
                i = i + 1;
            } 
            else 
            {
                if argi < count 
                {
                    builder.append(format_obj(data[argi]));
                    argi = argi + 1;
                }
                else builder.append("null", 4);
            }
        }
        else 
        {
            builder.append(fmt[i]);
            i = i + 1;
        }
    }
    ret builder.build();
}
fn format(fmt: *char, data: *List): *char 
{
    ret format(fmt, data.data, data.count);
}
fn arr(first: Obj): *List
{
    let array = cast((box new List(4)).data, *List);
    array.add(first);
    ret array;
}
fn writef(fmt: *char, data: **void) 
{
    writestr(format(fmt, data));
}
