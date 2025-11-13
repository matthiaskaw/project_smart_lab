import matplotlib.pyplot as plt
import numpy as np
import math
import sys
import os
import glob
from scipy import optimize
from scipy.optimize import curve_fit, fsolve

import scipy
import matplotlib.pyplot as plt

elementary_charge = 1.602176633e-19
air_viscosity = 1.72e-5



def calculate_diameter_from_mobility(mobility, d0 = 14e-9, xtol=1e-12, disp=0):

    diameter = optimize.fmin(Z, d0, args=(mobility,), xtol = 1e-12, disp=0)[0]
    return diameter


def calculate_mobility_from_voltage(voltage, sheathflow, DMAType = "3085"):

    radius1 = get_radius1(DMAType)
    radius2 = get_radius2(DMAType)
    slitdistance = get_slitdistance(DMAType)

    return sheathflow/(2*math.pi*voltage*slitdistance)*math.log(radius2/radius1)
    

def calculate_mobility_from_voltage(voltage, sheathflow = 0.00025, DMA="3085"):
    
    if(DMA=="3085"):
        return sheathflow*math.log(1.905/0.937)/(2*math.pi*voltage*0.04987)
    else:
        return sheathflow*math.log(1.961/0.937)/(2*math.pi*voltage*0.44369)
    
def calculate_scantime_constant(minDiameter, maxDiameter, upscantime):

    return upscantime/math.log(maxDiameter/minDiameter)




def calculate_voltage_from_diameter(diameter, sheathflow = 0.00025, DMA="3085"):
    if(DMA=="3085"):
        return diameter*3*air_viscosity*sheathflow*math.log(1.905/0.937)/(2*elementary_charge*0.04987*cunningham_correction(diameter))
    elif(DMA=="3081"):
        return diameter*3*air_viscosity*sheathflow*math.log(1.961/0.937)/(2*elementary_charge*0.44369*cunningham_correction(diameter))
    else:
        return
def electrical_mobility_from_diameter(diameter, air_viscosity=air_viscosity, number_of_charges=1):
    

    return number_of_charges * elementary_charge *cunningham_correction(diameter) / (3 * np.pi * air_viscosity * diameter)


def calculate_voltage_from_time(measurementTime,
                    minVoltage = 16,
                    maxVoltage = 10000,
                    upscantime=120,
                    DMA="3085"):
    
        if(DMA=="3085"):
            deathtime = 0.34
            residencetime = 0.05
        else:
            deathtime = 1.7
            residencetime = 0.1
    
        
        nom = (measurementTime-deathtime-residencetime)
        denom = (upscantime-deathtime-residencetime)
       
        voltage = minVoltage*(maxVoltage/minVoltage)**(nom/denom)
     
        return voltage    


def charge_fraction(diameter):
    a = [-2.3484, 0.6044, 0.4800, 0.0013, -0.1553, 0.0320]
    exponents = [(a[i] * np.log10(diameter/1e-9) ** i) for i in range(0, len(a))]
    expo_array = np.array(exponents)

    exponents = sum(expo_array)
    f_final = 10 ** exponents  
    return f_final

def cunningham_correction(diameter, meanfreepath = 66*10**(-9)):
           
        return (1 + (meanfreepath/diameter)*(2.514+0.8*math.exp(-0.55*diameter/meanfreepath)))
     



def dispositionParameter(diameter,
                            length = 13.97,
                            temperature=298,
                            viscosity=17.2*10**(-6),
                            volumetricFlow=0.000025):


    boltzmannconstant = 1.38064852*10**(-23)
    factor = boltzmannconstant*temperature*length/(3*math.pi*viscosity*volumetricFlow)
    cunningham = 1 + (66*10**(-9)/diameter)*(2.34+1.05*math.exp(-0.39*diameter/66*10**(-9)))
        
    return factor*cunningham/diameter

def fuchs_correction_diffusion(diameter, lambda_air = 66e-9):
    return ( 2 * lambda_air + diameter) / ( diameter + 5.33 * (lambda_air ** 2/ diameter) + 3.42 * lambda_air)   
   
def droplet_evaporation_diffusion(x_droplet, T_droplet, p_droplet, p_ambient, T_ambient, D_v, M, rho_droplet):
    '''Diffusion controlled evaporation dx/dt'''    

    fac = 4 * D_v * M / (rho_droplet * 8.314)

    return fac / x_droplet * (p_ambient / T_ambient - p_droplet / T_droplet) * fuchs_correction_diffusion(x_droplet)




def rayleigh_limit(diameter, gamma = 0.072):
    return np.pi * ( 8 * gamma * 8.85e-12 * diameter ** 3) **0.5


def Reynolds(v, diameter):
    return 1.2 * v * diameter /1.72e-5

def refineSMPSSpectra(spectra):
    refinedSpectra = []
    for j in spectra:
        if(j.find("OK") == -1 and j.find("-1") == -1 and j.find("-") == -1):
            j.replace("-1","")
            j.replace("-","")
            refinedSpectra.append(j)
                
    return refinedSpectra
 

def fillSpectra(spectra):
    maxElements = 0
    for i in spectra:
        if(len(i)>maxElements):
            maxElements = len(i) - 1
    
    filledSpectra = []
    for i in spectra:
        for j in range(len(i)-1, maxElements):
            i.append(0)
        filledSpectra.append(i)
        
    return filledSpectra



    
def penetration(dispParameter):
    
    if(dispParameter < 0.009):
        return 1-5.5*dispParameter**(2/3)+3.77*dispParameter
    else:
        return 0.819*math.exp(-11.5*dispParameter)+0.0975*math.exp(-70.1*dispParameter)
 


def Z(d,z):
    e0 = 1.602176634*10**(-19)
    mu = 17.2*10**(-6)
    n = 1
    
    fac = n*e0/(3*math.pi*mu) 
    return  ((fac*cunningham_correction(d)/d)-z)**2
    
    
def dummy (x,y):

    return x**2+y**2


  
def interpolateDiameter(xVector, yVector, numberOfPoints = 97):
    x_interpolated = np.logspace(math.log10(xVector[0]), math.log10(xVector[len(xVector)-1]), numberOfPoints)
    print(len(xVector), len(x_interpolated), len(yVector))
    return scipy.interpolate.interp1d(xVector,yVector, fill_value = "extrapolate")(x_interpolated)
    

def convertPSD(targetindex, currentindex, xVector, yVector):
        
        intervallWidth = [j-i for i,j in zip(xVector, yVector[1:])]
        qtarget = [(x**(targetindex-currentindex)*q)/()]



def get_slitdistance(DMAType):
    
    if(DMAType == "3085"):
        slitdistance = 0.04987
    else:
        slitdistance = 0.44369

    return slitdistance

def get_radius1(DMAType):
    
    if(DMAType == "3085"):
        radius1 = 0.00937
    else:
        radius1 = 0.00937
    return radius1

def get_radius2(DMAType):

    if(DMAType == "3085"):
        radius2 = 0.01905
    else:
        radius2 = 0.01961
    return radius2

def calculate_lowerBorder_mobility_from_voltage(voltage, aerosolflow, sheathflow, DMAType = "3085"):

    radius1 = get_radius1(DMAType)
    radius2 = get_radius2(DMAType)
    slitdistance = get_slitdistance(DMAType)

    return (1-aerosolflow/sheathflow)*sheathflow/(2*math.pi*voltage*slitdistance)*math.log(radius2/radius1)

def calculate_upperBorder_mobility_from_voltage(voltage, aerosolflow, sheathflow, DMAType = "3085"):

    radius1 = get_radius1(DMAType)
    radius2 = get_radius2(DMAType)
    slitdistance = get_slitdistance(DMAType)

    return (1+aerosolflow/sheathflow)*sheathflow/(2*math.pi*voltage*slitdistance)*math.log(radius2/radius1)

def get_timeacrossDMA(DMAType, sheathflow):

       
    if(DMAType == "3085"):
        radius1 = 0.00937
        radius2 = 0.01905
        slitdistance = 0.04987
    else:
        radius1 = 0.00937
        radius2 = 0.01961
        slitdistance = 0.44369

    timeacrossDMA = (radius2-radius1)**2*np.pi*slitdistance/sheathflow #t_tf
    return timeacrossDMA
    
def vapour_pressure(T, A, B, C):
    return 10 ** (A - B / (T + C))* 1e5

def kelvin_effect(temperature, diameter, surfaceTension, molarMass, density):
    return np.exp(4*surfaceTension*molarMass/(density*8.314*temperature*diameter))

def evaporation_dx(molecularMass, vapourpressure, kelvineffect, density, temperature, dt):
    nom = 2 * molecularMass * vapourpressure * kelvineffect
    denom = density * (2 * np.pi * molecularMass * 1.38064852e-23 * temperature) ** (1/2)
    

    return -nom/denom*dt

def fuchs_correction(diameter, freemeanpath = 66e-9):
    knudsen_number = 2 * freemeanpath / diameter
    return (1.33 * knudsen_number * (1 + knudsen_number)) / (1 + 1.71 * knudsen_number + 1.33 * knudsen_number ** 2)

def temperature_depression(T_droplet,
                           p_droplet,
                           p_ambient,
                           T_ambient,
                           diffusion_coefficient,
                           molar_mass,
                           enthalpy_of_vaporization,
                           heat_conductivity):
    
    fac = (diffusion_coefficient * molar_mass / ( heat_conductivity * 8.314) )
    

    return fac * enthalpy_of_vaporization * (p_droplet / T_droplet - p_ambient / T_ambient)

def solve_temperature_depression(                                
                                diffusion_coefficient,
                                molar_mass,
                                enthalpy_of_vaporization,
                                heat_conductivity,
                                p_ambient,
                                T_ambient,
                                A=5.40221,
                                B=1838.675,
                                C=-31.737,
                                T_initial_guess = 290):
    def func(T):
            
            return T - T_ambient - temperature_depression(T,
                                                        vapour_pressure(T, A, B, C),
                                                        p_ambient,
                                                        T_ambient,
                                                        diffusion_coefficient,
                                                        molar_mass,
                                                        enthalpy_of_vaporization,
                                                        heat_conductivity = heat_conductivity,
                                                        )

    T_initial_guess = 290
    T_droplet = fsolve(func, T_initial_guess)[0]
    return T_droplet

def main():

    print("Aerosol script testing:")

    print("Tesing slip correction:")
    print("Cunningham correction for 1 µm particle is 1.15.")
    cunHam = cunningham_correction(1e-6)
    print(f"Calculated Cunningham correction for 1µm is {cunHam}")
    print("*****")
    spectra= []

    with open(r".\testfiles\SMPS_2.4_68.5_17_120_15_0.txt") as f:
        
        spectra_lines = f.readlines()
        print("Starting testing refineSMPS():")
        spectra = refineSMPSSpectra(spectra_lines)
        spectra = [float(s.strip()) for s in spectra]
        
    print("Spectra in float values is:")
    print(spectra)
    print("*****")
    print("Testing calculate time function:")
    time = calculate_time(spectra)

    print(f"upscantime vector: {time} | {len(time)}")
    print("*****")
    mindiameter = 2.4e-9
    maxdiameter = 63.8e-9

    print(f"Testing Calculating voltage function of mindiameter ({mindiameter*1e9} nm) and maxdiameter ({maxdiameter*1e9} nm)")

    minVoltage = calculate_voltage_from_diameter(mindiameter)
    maxVoltage = calculate_voltage_from_diameter(maxdiameter)

    print("min voltage:", minVoltage)
    print("max voltage:", maxVoltage)
    print("*****")
    print("Testing calculating_voltage_from_time")
    voltage = [calculate_voltage_from_time(t, minVoltage, maxVoltage) for t in time]
    
    print(f"Min voltage of vector is {voltage[0]} | Max voltage of vector is {voltage[-1]}")
    print("*****")
    print(f"Testing calculate_mobility_from_voltage")
    mobility = [calculate_mobility_from_voltage(v,15/60000) for v in voltage]
    #Implement proper testing
    print("*****")
    print("Testing calculate_diameter_from_mobility")
    
    diameter = calculate_diameter_from_mobility(2.1e-6)
    diameter_vec = [calculate_diameter_from_mobility(m) for m in mobility]

    print("Diameter of a particle with a mobility of is 2.1e-6m²/Vs is 10nm")
    print(f"Diameter returned by function is {diameter} | Deviation of expected value = {round(abs(1-diameter/10e-9)*100, 2)}%")
    print("*****")
    hinds_diameter = [0.001e-6, 0.01e-6, 0.1e-6, 1.0e-6]
    p_hinds = [1.0, 0.428,0.029, 0.003]
    mu = [dispositionParameter(x) for x in hinds_diameter]

    p = [1-penetration(m) for m in mu]
    plt.figure(1)
    plt.plot(hinds_diameter, p_hinds, 'b-')
    plt.plot(hinds_diameter, p, 'ro')
    plt.xlabel("diameter [m]")
    plt.ylabel("fractional loss [-]")
    diameter_nm = [2.21, 4.53, 9.31, 19.11, 39.24, 60.43]
    charge_frac_man = [0.0082, 0.0170, 0.0380, 0.0810, 0.1467, 0.1857]
    charge_frac_calc = [charge_fraction(x*1e-9) for x in diameter_nm]
    plt.figure(2)
    plt.plot(diameter_nm, charge_frac_man, 'b-')
    plt.plot(diameter_nm, charge_frac_calc, 'ro')
    plt.xlabel("diameter [nm]")
    plt.ylabel("charge frac [-]")
    plt.show()
    
    





if __name__ =='__main__':
    print("Starting Testing:")
    main()