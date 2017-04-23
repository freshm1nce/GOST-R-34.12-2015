#include <string> 
#include "kyzneychik.h"
using namespace std;
int main(int argc, char* argv[]){
	string a1 = "encrypt";
	string a2 = "decrypt";
if (argv[2] == a1) func_encrypt(argv[1], argv[3], argv[4]);
	else 
		if (argv[2] == a2) func_decrypt(argv[1], argv[3], argv[4]); 
	else 
		cout<<"error"<<endl;		
return 0;
}
