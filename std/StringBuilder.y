struct StringBuilder 
{
    data: *char;
    count: u64;
    size: u64;
    constructor() 
    {
        this.data = cast(new_array(128, typeof(char)).data, *char);
        this.size = 128;
        this.count = 0;
        let i: u64 = 0;
        while i < this.size 
        {
            this.data[i] = cast(0, char);
            i = i + 1;
        }
    }
    fn append(ch: char): *StringBuilder
    {
        if this.count == this.size this.grow();
        this.data[this.count] = ch;
        this.count = this.count + 1;
        ret this;
    }
    fn append(str: *char, len: u64): *StringBuilder 
    {
        if (this.count + len) >= this.size 
        {
            this.grow();
            
        }
        let i: u64 = 0;
        while i < len 
        {
            this.data[this.count + i] = str[i];
            i = i + 1;
        }
        this.count = this.count + i;
        
        ret this;
    }
    fn append(str: *char): *StringBuilder 
    {
        let len: u64 = cast(strlen(str), u64);
        ret this.append(str, len);
    }
    fn append(x: u64): *StringBuilder
    {
        if x == 0
        {
            this.append('0');
            ret this;
        }
        let buff: *char = cast(new_obj(64, typeof(char)).data, *char);
        let secondbuff: *char = cast(new_obj(64, typeof(char)).data, *char);
        let i: u64 = 0;
        let index: u64 = 0;
        while x > 0 
        {
            let rem = x % 10;
            buff[index] = cast(rem + 48, char);
            index = index + 1;
            x = x / 10;
        }
        let len = index;
        index = index - 1;
        while index >= 0
        {
            secondbuff[i] = buff[index];
            index = index - 1;
            i = i + 1;
        }
        this.append(secondbuff, len);
        ret this;
    }
    fn append(x: i32): *StringBuilder
    {
        if x == 0
        {
            this.append('0');
            ret this;
        }
        let buff: *char = cast(new_obj(64, typeof(char)), *char);
        let secondbuff: *char = cast(new_obj(64, typeof(char)), *char);
        let i: u64 = 0;
        let index: u64 = 0;
        let wasneg = false;
        if x < 0
        {
            x = -x;
            wasneg = true;
        }
        while x > 0 
        {
            let rem = x % 10;
            buff[index] = cast(rem + 48, char);
            index = index + 1;
            x = x / 10;
        }
        if(wasneg)
        {
            buff[index] = '-';
            index = index + 1;
        }
        let len = index;
        index = index - 1;
        while index >= 0
        {
            secondbuff[i] = buff[index];
            index = index - 1;
            i = i + 1;
        }
        this.append(secondbuff, len);
        ret this;
    }
    fn endl(): *StringBuilder
    {
        this.append("\r\n", cast(2, u64));
        ret this;
    }
    fn build(): *char
    {
        let string = cast(new_array(this.count + 1, typeof(char)).data, *char);
        let i: u64 = 0;
        while i < this.count 
        {
            string[i] = this.data[i];
            i = i + 1;
        }
        string[i] = cast(0, char);
        ret string;
    }
    fn write() 
    {
        writestr(this.data, this.count);
    }
    fn grow()
    {
        let newsize = this.size * 2;
        let newdata = cast(new_array(newsize, typeof(char)).data, *char);
        let i: u64 = 0;
        while i < this.size 
        {
            newdata[i] = this.data[i];
            i = i + 1;
        }
        this.data = newdata;
        this.size = newsize;
    }
    fn clear() 
    {
        this.count = 0;
    }
}
