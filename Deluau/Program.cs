using Deluau.Luau;
using Deluau.Reader;

string FilePath = "C:\\Users\\water\\RiderProjects\\Deluau\\Deluau\\Test\\Maid.luac";
using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
{ 
    State state = new State(stream);
}