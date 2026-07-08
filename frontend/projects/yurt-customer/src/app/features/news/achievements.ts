import { CustomerStats } from 'shared-models';

export interface Achievement {
  id: string;
  icon: string;
  nameEn: string;
  nameRu: string;
  nameKk: string;
  descEn: string;
  descRu: string;
  descKk: string;
  condition: (stats: CustomerStats, flags: Record<string, string | null>) => boolean;
}

export const ACHIEVEMENTS: Achievement[] = [
  {
    id: 'first_sip',
    icon: '☕',
    nameEn: 'First Sip',
    nameRu: 'Первый Глоток',
    nameKk: 'Бірінші Кесе',
    descEn: 'Placed your first order',
    descRu: 'Сделали первый заказ',
    descKk: 'Алғашқы тапсырысыңызды бердіңіз',
    condition: (s) => s.totalOrders >= 1,
  },
  {
    id: 'coffee_rookie',
    icon: '🥉',
    nameEn: 'Coffee Rookie',
    nameRu: 'Кофе Новичок',
    nameKk: 'Кофе Жаңадан',
    descEn: '3 orders completed',
    descRu: '3 заказа выполнено',
    descKk: '3 тапсырыс орындалды',
    condition: (s) => s.totalOrders >= 3,
  },
  {
    id: 'golden_cup',
    icon: '🥇',
    nameEn: 'Golden Cup',
    nameRu: 'Золотая Чаша',
    nameKk: 'Алтын Кесе',
    descEn: '10 orders — a loyal regular',
    descRu: '10 заказов — верный постоянный',
    descKk: '10 тапсырыс — адал тұрақты',
    condition: (s) => s.totalOrders >= 10,
  },
  {
    id: 'altyn_regular',
    icon: '💛',
    nameEn: 'Altyn Regular',
    nameRu: 'Постоянный',
    nameKk: 'Тұрақты',
    descEn: '25 orders — true Altyncup fan',
    descRu: '25 заказов — настоящий фанат',
    descKk: '25 тапсырыс — шынайы жанкүйер',
    condition: (s) => s.totalOrders >= 25,
  },
  {
    id: 'altyncup_star',
    icon: '🌟',
    nameEn: 'Altyncup Star',
    nameRu: 'Звезда Алтынкап',
    nameKk: 'Алтынкап Жұлдызы',
    descEn: '50 orders — a shining star',
    descRu: '50 заказов — сияющая звезда',
    descKk: '50 тапсырыс — жарқын жұлдыз',
    condition: (s) => s.totalOrders >= 50,
  },
  {
    id: 'century_sipper',
    icon: '💯',
    nameEn: 'Century Sipper',
    nameRu: '100 Чашек',
    nameKk: '100 Кесе',
    descEn: '100 orders — legendary',
    descRu: '100 заказов — легендарный',
    descKk: '100 тапсырыс — аңызға айналған',
    condition: (s) => s.totalOrders >= 100,
  },
  {
    id: 'big_spender',
    icon: '💰',
    nameEn: 'Coffee Baron',
    nameRu: 'Кофейный Барон',
    nameKk: 'Кофе Барон',
    descEn: 'Spent over 10,000 ₸',
    descRu: 'Потрачено более 10 000 ₸',
    descKk: '10 000 ₸-тен артық жұмсалды',
    condition: (s) => s.totalSpent >= 10000,
  },
  {
    id: 'altyn_champion',
    icon: '👑',
    nameEn: 'Altyn Champion',
    nameRu: 'Чемпион',
    nameKk: 'Алтынкап Чемпион',
    descEn: 'Spent over 50,000 ₸',
    descRu: 'Потрачено более 50 000 ₸',
    descKk: '50 000 ₸-тен артық жұмсалды',
    condition: (s) => s.totalSpent >= 50000,
  },
  {
    id: 'wolt_rider',
    icon: '🛵',
    nameEn: 'Wolt Rider',
    nameRu: 'Курьер Wolt',
    nameKk: 'Wolt Жеткізуші',
    descEn: 'Used Wolt delivery',
    descRu: 'Воспользовались доставкой Wolt',
    descKk: 'Wolt жеткізуін пайдаландыңыз',
    condition: (_s, flags) => flags['wolt'] === 'true',
  },
];
