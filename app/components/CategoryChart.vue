<template>
  <div class="w-full h-full">
    <canvas ref="chartRef"></canvas>
  </div>
</template>

<script setup lang="ts">
interface Props {
  data: Array<{ name: string; amount: number }>
}

const props = defineProps<Props>()
const chartRef = ref<HTMLCanvasElement>()
let chartInstance: any = null

// Colors for the chart
const colors = [
  '#3B82F6', // blue-500
  '#EF4444', // red-500
  '#10B981', // emerald-500
  '#F59E0B', // amber-500
  '#8B5CF6', // violet-500
  '#EC4899', // pink-500
  '#06B6D4', // cyan-500
  '#84CC16', // lime-500
]

onMounted(async () => {
  // Dynamic import of Chart.js to avoid SSR issues
  const { Chart, registerables } = await import('chart.js')
  Chart.register(...registerables)

  if (chartRef.value) {
    chartInstance = new Chart(chartRef.value, {
      type: 'doughnut',
      data: {
        labels: props.data.map(item => item.name),
        datasets: [{
          data: props.data.map(item => item.amount),
          backgroundColor: colors.slice(0, props.data.length),
          borderWidth: 0,
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              usePointStyle: true,
              padding: 15,
              font: {
                size: 12
              }
            }
          },
          tooltip: {
            callbacks: {
              label: (context) => {
                const value = context.parsed
                const total = props.data.reduce((sum, item) => sum + item.amount, 0)
                const percentage = ((value / total) * 100).toFixed(1)
                return `${context.label}: $${value.toLocaleString()} (${percentage}%)`
              }
            }
          }
        },
        cutout: '60%',
      }
    })
  }
})

onUnmounted(() => {
  if (chartInstance) {
    chartInstance.destroy()
  }
})

watch(() => props.data, () => {
  if (chartInstance) {
    chartInstance.data.labels = props.data.map(item => item.name)
    chartInstance.data.datasets[0].data = props.data.map(item => item.amount)
    chartInstance.data.datasets[0].backgroundColor = colors.slice(0, props.data.length)
    chartInstance.update()
  }
}, { deep: true })
</script>