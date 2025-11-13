import matplotlib.pyplot as plt
import numpy as np
from matplotlib.ticker import FormatStrFormatter,LogFormatter, MultipleLocator, AutoMinorLocator, FixedLocator, FixedFormatter, MaxNLocator, LogLocator
from matplotlib.colors import LogNorm

from matplotlib import cm
from matplotlib.colors import ListedColormap

from matplotlib.ticker import ScalarFormatter

def gaussian(x, expectation_value, standarddeviation):
    
    return 1 / (standarddeviation * (2 * np.pi) ** 0.5) * np.exp(-0.5 * (x - expectation_value) ** 2 / standarddeviation ** 2)


def format_plot(fig):

    fontsize = 14
    linewidth = 3
    ax = fig.gca()
    xlab = ax.get_xlabel()
    ylab = ax.get_ylabel()
    ax.set_xlabel(xlab,fontsize=fontsize, fontweight='bold')
    ax.set_ylabel(ylab,fontsize=fontsize, fontweight='bold')
    plt.xticks(fontweight='bold', fontsize=fontsize)
    plt.yticks(fontweight='bold', fontsize=fontsize)
    for axis in ['top','bottom','left','right']:
        ax.spines[axis].set_linewidth(linewidth)
    ax.legend(prop={'weight':'bold', 'size':f'{fontsize/1.2}'}, loc='best')
    lines = ax.get_lines()
    [l.set_linewidth(linewidth) for l in lines]
    ax.xaxis.set_tick_params(width=3, length=9)
    ax.tick_params(axis='x', which='minor', length=6, width=1)
    ax.tick_params(axis='y', which='minor', length=6, width=1)

    ax.yaxis.set_tick_params(width=3, length=9)
    ax.tick_params(which='minor', length=4, width=1)
    
    print("Formatting")

def format_subplot(fig):
    fontsize = 14
    linewidth = 3
    for ax in fig.axes:
        # Example formatting
        for line in ax.get_lines():
            line.set_linewidth(linewidth)
            line.set_markersize(6)
        
        for label in ax.get_xticklabels():

            label.set_fontweight('bold')
            label.set_fontsize(fontsize)
        
        for label in ax.get_yticklabels():

            label.set_fontweight('bold')
            label.set_fontsize(fontsize)


            # Axis settings
        ax.tick_params(width=3,direction='out', length=9)
        for axis in ['top','bottom','left','right']:
            ax.spines[axis].set_linewidth(linewidth)
        ax.grid(False)
        xlab = ax.get_xlabel()
        ylab = ax.get_ylabel()
        ax.set_xlabel(xlab,fontsize=fontsize, fontweight='bold')
        ax.set_ylabel(ylab,fontsize=fontsize, fontweight='bold')

        # Font sizes
        ax.title.set_fontsize(12)
        ax.xaxis.label.set_fontsize(12)
        ax.yaxis.label.set_fontsize(12)
    
        #ax.tick_params(labelsize=10)


def get_jet_colormap():
 
    jet = cm.get_cmap('jet', 256)  # 256 colors
    jet_colors = jet(np.linspace(0, 1, 256))

    # Modify the first color (corresponding to lowest value) to white
    jet_colors[0] = [1, 1, 1, 1]  # RGBA for white

    # Create a new colormap from the modified array
    return ListedColormap(jet_colors)

def format_surf_plot(fig, pcm, colorbar_label='Z-Werte' ):
    
    
    fontsize = 14
    linewidth = 3
    width = 3
    length = 9
    ax = fig.gca()

    jet = cm.get_cmap('jet', 256)  # 256 colors
    jet_colors = jet(np.linspace(0, 1, 256))

    # Modify the first color (corresponding to lowest value) to white
    jet_colors[0] = [1, 1, 1, 1]  # RGBA for white

    # Create a new colormap from the modified array
    jet_white = ListedColormap(jet_colors)
    pcm.set_cmap(jet_white)
    cbar = fig.colorbar(pcm, ax=ax, label=colorbar_label)
    cbar.ax.tick_params(width=width, length=length, labelsize=fontsize, direction="inout")
    cbar.set_label(colorbar_label, fontsize=14, fontweight='bold')
    for label in cbar.ax.get_yticklabels():  # oder get_xticklabels() je nach Orientierung
        label.set_fontweight('bold')

    for spine in cbar.ax.spines.values():
        spine.set_linewidth(linewidth)
    xlab = ax.get_xlabel()
    ylab = ax.get_ylabel()
    ax.tick_params(axis='y', labelleft=True)  # Must be True to show labels



    # ax.xaxis.set_major_formatter(LogFormatter())
    # ax.yaxis.set_major_formatter(LogFormatter())
    ax.set_xlabel(xlab,fontsize=fontsize, fontweight='bold')
    ax.set_ylabel(ylab,fontsize=fontsize, fontweight='bold')
    plt.xticks(fontweight='bold', fontsize=fontsize)
    plt.yticks(fontweight='bold', fontsize=fontsize)
    for axis in ['top','bottom','left','right']:
        ax.spines[axis].set_linewidth(linewidth)
    ax.legend(prop={'weight':'bold', 'size':f'{fontsize/1.2}'}, loc='best')
    lines = ax.get_lines()
    [l.set_linewidth(linewidth) for l in lines]
  

    #ax.set_xscale('log')
    #ax.set_yscale('log')
 
    # ax.yaxis.set_major_locator(LogLocator(base=2))
    # ax.yaxis.set_major_formatter(LogFormatter(base=2))
    # ax.yaxis.set_minor_locator(LogLocator(base=2))
    # ax.yaxis.set_minor_formatter(LogFormatter(base=2))
  
    ax.tick_params(axis='x', which='minor', length=6, width=1)
    ax.tick_params(axis='y', which='minor', length=6, width=1)
    ax.xaxis.set_tick_params(width=width, length=length)
    ax.yaxis.set_tick_params(width=width, length=length)
    # ax.xaxis.set_major_locator(MaxNLocator(integer=True))  # ganze Zahlen als Ticks
    # ax.yaxis.set_major_locator(MaxNLocator(integer=True))  # ganze Zahlen als Ticks
    ax.legend().remove()
    
def main():
    



    x = np.linspace(0, 4*np.pi, 1000)
    y1 = np.sin(x)
    y2 = np.sin(2*x)
    fig = plt.figure(1)
    
    
    plt.plot(x,y1)
    plt.plot(x,y2)
    format_plot(fig)
    plt.show()
    print("Testing")
    

    diameter = np.logspace(0,2, 100)
    current = np.logspace(0,np.log10(96),100)
    zvalues = np.zeros((diameter.shape[0], current.shape[0]))
    fig = plt.figure(2, figsize=(8,6), layout="constrained")

    pcm = plt.pcolormesh(current, diameter, zvalues)  # auto adjusts for sizes
    format_surf_plot(fig, pcm)
    plt.yscale('log')
    plt.xscale('log')
    plt.show()
        



if __name__ == '__main__':
    main()