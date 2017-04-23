#ifndef KYZNEYCHIK_H
#define KYZNEYCHIK_H
#include <fstream>
#include "table.h"
#include <iostream>
#include <string>
#include <cstdlib>
#include "shaaa.h"
using namespace std;

void copy(unsigned char* a, unsigned char* b)
{
	for (char j=0; j<16; j++)
		a[j] = b[j];
}

class kyzneychik
{
	unsigned char K1[16];
	unsigned char K2[16];
	unsigned char K3[16];
	unsigned char K4[16];
	unsigned char K5[16];
	unsigned char K6[16];
	unsigned char K7[16];
	unsigned char K8[16];
	unsigned char K9[16];
	unsigned char K10[16];
	unsigned char* KEYS[10] = {K1, K2, K3, K4, K5, K6, K7, K8, K9, K10};
	unsigned char MASTER_KEY[32];	
public:
//конструктор для выработки ключа
	kyzneychik(string c)
	{		
		string c1 = sha256(c); //хэш пароля в формате строки
	    int i,j;
	    string c2;
	    j = 0;
	    for( int k=0; k<64; k+=2)
	    {
			c2 = c1.substr(k,2);
			i = strtol(c2.c_str(), NULL, 16);
			MASTER_KEY[j] = (unsigned char) i;
			j++;
		}
		func_keys();
	}

//преобразование X
	void funcX(unsigned char* a, unsigned char* b, unsigned char* out)
	{
		for (int i = 0; i < 16; ++i)
		{
			out[i] = a[i] ^ b[i];
		}
	}

//преобразование S подстановки
	void funcS(unsigned char* in, unsigned char* out)
	{
		for (int i = 0; i < 16; ++i) 
			out[i] = kPi[in[i]];
	}

//преобразование, обратной подстановки S^(-1)
	void funcSreverse(unsigned char* in, unsigned char* out)
	{
		for (int i = 0; i < 16; ++i) 
			out[i] = kReversePi[in[i]];
	}

//преобразование l 
	unsigned char funcl(unsigned char* in)
	{
		unsigned long sum = 0;
		for (int i=0; i<16; i++)
			sum^= multTable[in[i] * 256 + kB[i]];
		return sum;
	}

//преобразование R	
	void funcR(unsigned char* in, unsigned char* out)
	{
		int i;
		for (i =0; i<15; i++)
			out[i+1] = in[i];
		out[0] = funcl (in);
	}

//преобразование R^(-1)
	void funcRreverse(unsigned char* in, unsigned char* out)
	{
		int i;
		unsigned char temp = in[0];
		for (i = 0; i<16; i++)
			if(i!=15) out[i] = in[i+1];
		for (i=0; i<15; i++)
			in[i] = in[i+1];
		in[15] = temp;
		out[15] = funcl (in);
	}

//преобразование L
	void funcL(unsigned char* in, unsigned char* out)
	{
		unsigned char temp[16] = {0};
		copy(temp,in);
		for (int i=0; i<16; i++)
		{
			funcR(temp, out);
		copy(temp,out);
		}
	}


//преобразование L^(-1)
	void funcLreverse(unsigned char* in, unsigned char* out)
	{
		unsigned char temp[16] = {0};
		copy(temp,in);
		for (int i=0; i<16; i++)
		{
			funcRreverse(temp, out);
			copy(temp,out);
		}
	}
/*для констант(необязательно)
	void funcC(int i, unsigned char* out){
		unsigned char temp[16] = {0, 0, 0, 0, 0,0, 0, 0, 0, 0,0, 0, 0, 0, 0, i};
		funcL(temp, out);
	}
*/	

//преобразование LSX
	void funcLSX(unsigned char* in1, unsigned char* in2, unsigned char* out)
	{
		unsigned char temp[16] = {0x0};
		funcX(in1, in2, temp);
		funcS(temp, temp);
		funcL(temp, temp);
		copy(out,temp);	
	}

//преобразование процесса расшифровки
	void funcLSXreverse(unsigned char* in1, unsigned char* in2, unsigned char* out) {
		unsigned char temp[16] = {0};
		funcX(in1, in2, temp);
		funcLreverse(temp, temp);
		funcSreverse(temp, temp);
		copy(out,temp);
	}

//функция F для развертки ключа
	void funcF(unsigned char* in1, unsigned char* in2, unsigned char* out1, unsigned char* out2, int k)
	{
		unsigned char temp[16] = {0};
		unsigned char temp1[16] = {0};
		unsigned char temp2[16] = {0};
		int i,j,k1;
		copy(temp,in1);
		copy(temp1,in2);
		k1=8*(k-1);
		for (j=0; j<2; j++)
		{
			copy(temp2,temp);
			k1++;		
			funcLSX(temp, C[k1-1], temp);
			funcX(temp, temp1, temp);	
			copy(temp1,temp);
			k1++;	
			funcLSX(temp, C[k1-1], temp);
			funcX(temp, temp2, temp);
			copy(temp2,temp);
			k1++;
			funcLSX(temp, C[k1-1], temp);
			funcX(temp, temp1, temp);
			copy(temp1,temp);
			k1++;
			funcLSX(temp, C[k1-1], temp);
			funcX(temp, temp2, temp);
		}
		copy(out1,temp);
		copy(out2,temp1);
	}		

//функция развертки ключа	
	void func_keys()
	{
		for (int i=0; i<16; i++)
			KEYS[0][i] = MASTER_KEY[i]; 
		for (int i=16; i<32; i++)
			KEYS[1][i-16] = MASTER_KEY[i]; 

		funcF(KEYS[0], KEYS[1], KEYS[2], KEYS[3], 1);
		funcF(KEYS[2], KEYS[3], KEYS[4], KEYS[5], 2);
		funcF(KEYS[4], KEYS[5], KEYS[6], KEYS[7], 3);
		funcF(KEYS[6], KEYS[7], KEYS[8], KEYS[9], 4);
	}
//функция шифрования в классе
	void encrypt()
	{
		funcX(OPEN_TEXT, R, OPEN_TEXT);
	    
	    for (int k=0; k<9; k++)
			funcLSX(OPEN_TEXT, KEYS[k], OPEN_TEXT);
		funcX(OPEN_TEXT,KEYS[9], SHIFR_TEXT);
	}
	void decrypt()
	{
		funcLSXreverse(SHIFR_TEXT, KEYS[9], OPEN_TEXT);
		for (int k=8; k>0; k--)
			funcLSXreverse(OPEN_TEXT, KEYS[k], OPEN_TEXT);
		funcX(OPEN_TEXT, KEYS[0], OPEN_TEXT);
		
		funcX(OPEN_TEXT, R, OPEN_TEXT);
	}	
};

//функция нахождения размера файла
	unsigned int func_f_size(char* a)
	{
		unsigned int size = 0;
		ifstream infile(a,ios::binary | ios::in );
	    infile.seekg (0, ios::end);
	    size = infile.tellg();
	    infile.close(); 
	    return size;
	}

//функция зашифрования
	void func_encrypt(char* a,char* b, char* c)
	{
		kyzneychik FIRST(c);
		unsigned int size = func_f_size(a)+4;
		ifstream infile(a, ios::binary | ios::in);
		ofstream outfile(b, ios::binary | ios::out);
		infile.seekg(0, ios::beg);

//синхрпосылка	
		ifstream IV("/dev/random", ios::binary | ios::in);
		for(int k=0; k<16; k++)
	  		if(IV)
	  		{    
	    		R[k] = IV.get();
	    	}
		IV.close();
		
		for(int k=0; k<16; k++)
			if(outfile) outfile.write((char*) &R[k], sizeof(unsigned char)); 


		unsigned char* x = (unsigned char*) &size;
// размер файла <16 байт	
		if (size/16==0)
		{
			char res = 16 - (size%16);
			if(res!=0) 
			{
				for(int k=0; k<16-res; k++)
	  				if ((infile)&&(k>=4)) 
	    				OPEN_TEXT[k] = infile.get();
	    			else
	    				if ((infile)&&(k<4))
	    					OPEN_TEXT[k] = x[k];
	    		OPEN_TEXT[16-res] = 0x1;	 
	    		for(int k=16-res+1; k<16; k++)
	    			OPEN_TEXT[k] = 0x0;

	    		FIRST.encrypt();
				
			for(int k=0; k<16; k++)
				if(outfile) outfile.write((char*) &SHIFR_TEXT[k], sizeof(unsigned char));				 					
			}	
		}
		else
		{	
//запись первого блока с размером
			for(int k=0; k<16; k++)
	  			if ((infile)&&(k>=4))   
	    			OPEN_TEXT[k] = infile.get();
	    		else
	    			if ((infile)&&(k<4))
	    				OPEN_TEXT[k] = x[k];
	    
	    	FIRST.encrypt();
			
			for(int k=0; k<16; k++)
				if(outfile)
					outfile.write((char*) &SHIFR_TEXT[k], sizeof(unsigned char));

			copy(R,SHIFR_TEXT);

			for(unsigned int j=1; j<size/16; j++)
			{		
				for(int k=0; k<16; k++)
	  				if(infile)
	  					OPEN_TEXT[k] = infile.get();
	    		    
	    		FIRST.encrypt();
					
				for(int k=0; k<16; k++)
					if(outfile)
						outfile.write((char*) &SHIFR_TEXT[k], sizeof(unsigned char)); 
				copy(R,SHIFR_TEXT);
			}
//c дополнением:
			char res = 16 - (size%16);
			if(res!=0)
			{ 
				for(int k=0; k<16-res; k++)
	  			if(infile)    
	    			OPEN_TEXT[k] = infile.get();
	    		OPEN_TEXT[16-res] = 0x1;	 
	    		for(int k=16-res+1; k<16; k++)
	    			OPEN_TEXT[k] = 0x0;

	    		FIRST.encrypt();
			
				for(int k=0; k<16; k++)
					if(outfile)
						outfile.write((char*) &SHIFR_TEXT[k], sizeof(unsigned char));	
			}	 					
		}	
	infile.close();
	outfile.close();
	}

//функция расшифрования
	void func_decrypt(char* a, char* b, char* c)
	{
		kyzneychik SECOND(c);
		ifstream infile(a,ios::binary | ios::in );
		unsigned int size = func_f_size(a) - 16;

//изъятие синхрпосылки	
		for(int k=0; k<16; k++)
  			if(infile)
  			{    
    			R[k] = infile.get();
    		}		

	    for(int k=0; k<16; k++)
	  		if(infile)
	  		{    
	    		SHIFR_TEXT[k] = infile.get();
	    	}
//дешифрование первого блока(в любом случае)
		SECOND.decrypt();

//считывание размера
		unsigned int size_temp;
		unsigned char* z = (unsigned char*) &size_temp;
		for (int i=0; i<4; i++)
			z[i] = OPEN_TEXT[i];

//Проверка ключа	
		if (size_temp>size)
		{
			cout<<"parol' ne tot"<<endl;
			return;
		}

		ofstream outfile(b, ios::binary | ios::out);
//если файл содержит только 1 блок		
		if (size/16==1)
		{
			char res=size-size_temp;
			res=16-res;
//запись в файл	
			for(int k=4; k<res; k++)
				if(outfile)
					outfile.write((char*) &OPEN_TEXT[k], sizeof(unsigned char));
			infile.close();
			outfile.close();
			return;
		}
		for(int k=4; k<16; k++)
			if(outfile)
				outfile.write((char*) &OPEN_TEXT[k], sizeof(unsigned char)); 

		copy(R,SHIFR_TEXT);

		for(unsigned int j=1; j<(size/16)-1; j++)
		{
		
			for(int k=0; k<16; k++)
		  		if(infile)
		  		{    
		    		SHIFR_TEXT[k] = infile.get();
		    	} 

			SECOND.decrypt();

			for(int k=0; k<16; k++)
				if(outfile)
					outfile.write((char*) &OPEN_TEXT[k], sizeof(unsigned char)); 

			copy(R,SHIFR_TEXT);
		}
//очищаем дополнение и запись размера файла
		char res=size-size_temp;
		res=16-res;

		for(int k=0; k<16; k++)
	  		if(infile)
	  		{    
    			SHIFR_TEXT[k] = infile.get();
    		}
//дешифрование    		
		SECOND.decrypt();

		for(int k=0; k<res; k++)
			if(outfile)
				outfile.write((char*) &OPEN_TEXT[k], sizeof(unsigned char)); 
		infile.close();
		outfile.close();
	}

#endif
