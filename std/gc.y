let stack_start: *u8 = null;
let SMALL_OBJ_SIZE: u64 = 32;
let MID_OBJ_SIZE: u64 = 128;
let BIG_OBJ_SIZE: u64 = 512;
let START_CHUNKS: u64 = 64;
let mark_size: u64 = 8;
let small_chunks: Chunks = new Chunks(64, 32);
let mid_chunks: Chunks = new Chunks(64, 128);
let big_chunks: Chunks = new Chunks(64, 512);
let variadic_chunks = new VariadicChunks();
let collect_count = 0;
struct VariadicChunk 
{
    is_ref: u16;
    is_free: u16;
    mark: i32;
    size: u64;
    data: *u8;
}
struct VariadicChunks 
{
    chunks: **VariadicChunk;
    size: u64;
    count: u64;
    constructor()
    {
        this.chunks = cast(alloc(24*32), **VariadicChunk);
        this.size = 16;
        this.count = 0;
        ret;
        this.add(cast(256, u64), false);
        this.add(cast(256, u64), false);
    }
    fn add(size: u64, is_ref: bool): *VariadicChunk
    {
        if this.size == this.count this.grow();
        size = next_pow_of2(size);
        let data = alloc(size);
        let chunk = cast(alloc(24), *VariadicChunk);
        if is_ref chunk.is_ref = 1; else chunk.is_ref = 0;
        chunk.is_free = 1;
        chunk.size = size;
        chunk.data = data;
        this.chunks[this.count] = chunk;
        this.count = this.count + 1;
        ret chunk;
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newdata = cast(alloc(newsize * 24), **VariadicChunk);
        let i: u64 = 0;
        while i < newsize 
        {
            newdata[i] = this.chunks[i];
            i = i + 1;
        }
        free(cast(this.chunks, *u8));
        this.chunks = newdata;
        this.size = newsize;
    }
}
struct FreePlaces 
{
    places: **u8;
    count: u64;
    size: u64;
    constructor() 
    {
        this.count = 0;
        this.size = 32;
        this.places = cast(alloc(this.size * 8), **u8);
    }
    fn add(place: *u8) 
    {
        if this.count == this.size this.grow();
        this.places[this.count] = place;
        this.count = this.count + 1;
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newplaces = cast(alloc(newsize * 8), **u8);
        let i: u64 = 0;
        while i < this.count {
            newplaces[i] = this.places[i];
            i = i + 1;
        }
        free(cast(this.places, *u8));
        this.places = newplaces;
        this.size = newsize;
    }
    fn pop(): *u8 
    {
        if this.count == 0 
        {
            writestr("\r\nNO FREE PLACES\r\n");
            asm { invoke ExitProcess, 0 }
        }
        this.count = this.count - 1;
        let res = this.places[this.count];
        ret res;
    }
}


struct Chunks 
{
    chunks: **u8;
    count: u64;
    size: u64;
    chunk_size: u64;
    free: FreePlaces;
    obj_size: u64;
    constructor(chunk_size: u64, obj_size: u64)
    {
        this.count = 0;
        this.size = START_CHUNKS;
        this.chunk_size = chunk_size;
        this.obj_size = obj_size + mark_size;
        this.chunks = cast(alloc(this.size * 8), **u8);
        this.free = new FreePlaces();
        this.newchunk();
    }
    fn newchunk() 
    {
        if this.count == this.size this.grow();
        let chunk = alloc(this.chunk_size * this.obj_size);
        let i: u64 = 0;
        while i < this.chunk_size * this.obj_size 
        {
            chunk[i] = 0;
            i = i + 1;
        }
        this.chunks[this.count] = chunk;
        this.count = this.count + 1;
        i = 0;
        while i < this.chunk_size 
        {
            this.free.add(cast(ptr_as_num(chunk) + i * this.obj_size, *u8));
            i = i + 1;
        }
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newchunks = cast(alloc(newsize * 8), **u8);
        let i: u64 = 0;
        while i < this.count {
            newchunks[i] = this.chunks[i];
            i = i + 1;
        }
        free(cast(this.chunks, *u8));
        this.chunks = newchunks;
        this.size = newsize;
    }
}
fn next_pow_of2(num: u64): u64
{
    num = num - 1;
    num = num | (num >> 1);
    num = num | (num >> 2);
    num = num | (num >> 4);
    num = num | (num >> 8);
    num = num | (num >> 16);
    num = num | (num >> 32);
    num = num + 1;
    ret num;
}
fn gc_collect() 
{
    mark_chunks(&small_chunks);
    mark_chunks(&mid_chunks);
    mark_chunks(&big_chunks);
    mark_in_variadic();
    mark_stack();
    if small_chunks.free.count == 0 gc_collect_chunk(&small_chunks);
    if mid_chunks.free.count == 0 gc_collect_chunk(&mid_chunks);
    if big_chunks.free.count == 0 gc_collect_chunk(&big_chunks);
    gc_collect_variadic();
    collect_count = collect_count + 1;
}
fn gc_collect_chunk(chunk: *Chunks) 
{
    let chunki: u64 = 0;
    while chunki < chunk.count 
    {
        let chunkb = chunk.chunks[chunki];
        let objptr = chunkb;
        while ptr_as_num(objptr) < (ptr_as_num(chunkb) + chunk.chunk_size * chunk.obj_size) 
        {
            let markptr = cast(objptr + chunk.obj_size - mark_size, *i32);
            if *markptr == 0 chunk.free.add(objptr);
            else *markptr = 0;
            objptr = objptr + chunk.obj_size;
        }
        chunki = chunki + 1;
    } 
}
fn gc_collect_variadic()
{
    let i: u64 = 0;
    while i < variadic_chunks.count 
    {
        let chunk = variadic_chunks.chunks[i];
        if chunk.mark == 0
        {
            chunk.is_free = 1;
            chunk.mark = 0;
        }
        else chunk.mark = 0;
        i = i + 1;
    }
}
fn mark_chunks(chunks: *Chunks) 
{
    let chunki: u64 = 0;
    while chunki < chunks.count 
    {
        let chunk = chunks.chunks[chunki];
        let obj: u64 = 0;
        while obj < chunks.chunk_size 
        {
            let objdata: u64 = 0;
            while objdata < chunks.obj_size - mark_size 
            {
                let objptr = cast(ptr_as_num(chunk) + obj * chunks.obj_size + objdata, **u8);
                if mark_obj(*objptr, &small_chunks) {}
                else if mark_obj(*objptr, &mid_chunks) {}
                else if mark_obj(*objptr, &big_chunks) {}
                else mark_variadic(*objptr);
                objdata = objdata + 8;
            }
            obj = obj + 1;
        }
        chunki = chunki + 1;
    }
}
fn mark_stack() 
{
    let rsp: u64 = 0;
    asm 
    {
        mov [rbp - 8], rsp
    }
    let stack = ptr_as_num(stack_start);
    while stack > rsp 
    {
        if mark_obj(*cast(stack, **u8), &small_chunks) {}
        else if mark_obj(*cast(stack, **u8), &mid_chunks) {}
        else if mark_obj(*cast(stack, **u8), &big_chunks) {}
        else mark_variadic(*cast(stack, **u8));
        stack = stack - 8;
    }
}
fn mark_in_variadic()
{
    let i: u64 = 0;
    while i < variadic_chunks.count 
    {
        let chunk = variadic_chunks.chunks[i];
        if chunk.is_free == 0 && chunk.is_ref == 1 
        {
            let objptr: u64 = 0;
            while objptr < chunk.size 
            {
                let ptr = objptr + ptr_as_num(chunk.data);
                if mark_obj(*cast(ptr, **u8), &small_chunks) {}
                else if mark_obj(*cast(ptr, **u8), &mid_chunks) {}
                else if mark_obj(*cast(ptr, **u8), &big_chunks) {}
                else mark_variadic(*cast(ptr, **u8));
                objptr = objptr + 8;
            }
        }
        i = i + 1;
    }
}
fn mark_variadic(obj: *u8) 
{
    let i: u64 = 0;
    while i < variadic_chunks.count 
    {
        let chunk = variadic_chunks.chunks[i]; 
            let dataptr = ptr_as_num(chunk.data);
            let ptr = ptr_as_num(obj);
            if ptr == dataptr chunk.mark = 1;
        i = i + 1;
    }
}
fn set_start() 
{
    asm 
    {
        mov rax, [rsp]
        mov qword[stack_start], rax
    }
}
fn mark_obj(ptr: *u8, chunk: *Chunks): bool 
{
    let i: u64 = 0;
    while i < chunk.count
    {
        let chunkb = chunk.chunks[i];
        let ptrnum = ptr_as_num(ptr);
        let chunk_start = ptr_as_num(chunkb);
        let chunk_end = ptr_as_num(chunkb) + chunk.chunk_size * chunk.obj_size;
        if ptrnum >= chunk_start && ptrnum < chunk_end 
        {
            let mark_ptr = cast(ptrnum + chunk.obj_size - mark_size, *i32);
            *mark_ptr = 1;
            ret true;
        }
        i = i + 1;
    }
    ret false;
}
fn ptr_as_num(ptr: *u8): u64
{
    ret cast(ptr, u64);
}
fn new_obj(chunks: *Chunks): *u8 
{
    if chunks.free.count > 0 
    {
        ret chunks.free.pop();
    }
    else 
    {
        gc_collect();
        if chunks.free.count == 0 chunks.newchunk();
        ret chunks.free.pop();
    }
}
fn new_variadic(size: u64, is_ref: bool): *u8 
{
    let i: u64 = 0;
    while i < variadic_chunks.count 
    {
        let chunk = variadic_chunks.chunks[i];    
        if chunk.size >= size && chunk.is_free == 1 
        {
            chunk.is_free = 0;
            ret chunk.data;
        }
        i = i + 1;
    } 
    gc_collect();
    i = 0;
    while i < variadic_chunks.count 
    {
        chunk = variadic_chunks.chunks[i];    
        if chunk.size >= size && chunk.is_free == 1 
        {
            chunk.is_free = 0;
            ret chunk.data;
        }
        i = i + 1;
    } 
    let ch = variadic_chunks.add(size, is_ref);
    ch.is_free = 0;
    ret ch.data;
}
fn new_obj(size: u64): *u8 
{
    if size <= SMALL_OBJ_SIZE ret new_obj(&small_chunks);
    else if size > SMALL_OBJ_SIZE && size <= MID_OBJ_SIZE ret new_obj(&mid_chunks);
    else if size > MID_OBJ_SIZE && size <= BIG_OBJ_SIZE ret new_obj(&big_chunks);
    else ret new_variadic(size, true);
    ret null;
}
fn new_array(size: u64): *u8
{
    ret new_variadic(size, false);
}
fn gc_init() 
{
    asm 
    {
        mov [stack_start], rsp
    }
}
