using Khod;


//string words = "ad homing abracadabra hello world";// motherboard adbdcdadbdcd";
//string words = "hello world";
string words = "auchtertool adbdcdadbdcd";
string path = "kohd1.html";


string translated = String.Join("", words.Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(x => new KhodWord(x)).AsParallel());


const string HTML_HEADER = "<!DOCTYPE html>\n<html>\n<body>\n";
const string HTML_FOOTER = "</body>\n</html>";

string outputString = HTML_HEADER + translated + HTML_FOOTER;

File.WriteAllText(path, outputString);
