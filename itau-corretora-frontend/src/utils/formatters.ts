
export const formatarMoeda = (valor: number): string => {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(valor);
};
export const maskCPF = (value: string): string => {
  return value
    .replace(/\D/g, '') 
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d)/, '$1.$2') 
    .replace(/(\d{3})(\d{1,2})/, '$1-$2') 
    .replace(/(-\d{2})\d+?$/, '$1');
};

export const maskCurrencyInput = (value: string): string => {
  const onlyNumbers = value.replace(/\D/g, '');
  if (!onlyNumbers) return '';
  const amount = parseFloat(onlyNumbers) / 100;
  return amount.toLocaleString('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  });
};
export const unmaskCurrency = (value: string): number => {
  const onlyNumbers = value.replace(/\D/g, '');
  return parseFloat(onlyNumbers) / 100;
};