struct List 
{
    data: *Obj;
    count: u64;
    size: u64;
    constructor(size: u64) 
    {
        this.size = size;
        this.count = 0;
        this.data = cast(new_array(this.size * typeof(Obj).size, typeof(Obj)).data, *Obj);
    }
    fn add(obj: Obj): *List
    {
        if this.count == this.size this.grow();
        this.data[this.count] = obj;
        this.count = this.count + 1;
        ret this;
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newdata = cast(new_array(newsize * typeof(Obj).size, typeof(Obj)).data, *Obj);
        let i: u64 = 0;
        while i < this.size 
        {
            newdata[i] = this.data[i];
            i = i + 1;
        }
        this.size = newsize;
        this.data = newdata;
    }
}
